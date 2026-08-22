# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Krilla is a .NET wrapper over [krilla](https://github.com/LaurenzV/krilla), the Rust library that writes PDFs for typst. Unlike every other project in Papyrine, this one contains a native component it builds itself: krilla has no C API, so `rust/crates/krilla-capi` is a `cdylib` shim written here, and `src/Krilla` P/Invokes it. The public API lives in the `Krilla` namespace with `KrillaDocument` as the entry point.

`src/Krilla.Html` sits on top of it and converts HTML to PDF: AngleSharp parses and cascades, `Layout/` positions boxes, `Painting/` draws them through krilla. It is a layout engine under construction, so its corpus harness (`src/Krilla.Html.Tests/Inputs`) matters as much as the engine — see [The HTML converter](#the-html-converter-srckrillahtml).

## Build & Test Commands

Tests use **TUnit**, not VSTest. `dotnet test` is unsupported on the .NET 10 SDK and will error. Use `dotnet run`, and TUnit's `--treenode-filter` (not `--filter`) to narrow:

```bash
# Managed build. Shells out to cargo automatically when the native is missing or stale.
dotnet build src --configuration Release

# Managed tests
dotnet run --project src/Krilla.Tests --configuration Release

# One class
dotnet run --project src/Krilla.Tests --configuration Release -- --treenode-filter "/*/*/DocumentTests/*"

# HTML converter tests, including the whole scenario corpus
dotnet run --project src/Krilla.Html.Tests --configuration Release

# One corpus category
dotnet run --project src/Krilla.Html.Tests --configuration Release -- --treenode-filter "/*/*/BlockCorpusTests/*"

# Regenerate the browser references. Explicit, and never run as part of a test run — see below.
dotnet run --project src/Krilla.Html.RefGen -- --treenode-filter "/*/*/ReferenceGenerator/*"

# Rust tests
cargo test --manifest-path rust/Cargo.toml

# Rust tests under Miri. This is the check that matters for src/document.rs; run it after
# any change to the lifetime-erasure code.
cargo +nightly miri test --manifest-path rust/Cargo.toml
```

Unlike the other Papyrine repositories, this one does not reference SponsorCheck, so a `Release` build needs no GitHub token and packs without extra arguments.

Two MSBuild switches exist for CI and are worth knowing locally:

- `-p:KrillaSkipCargo=true` — the native already exists; do not shell out to cargo. Used wherever the binary was built separately with specific `RUSTFLAGS` that a rebuild would drop.
- `-p:KrillaRequireAllNatives=true` — a missing RID becomes an error instead of a warning. Set only in the release pack job, so a partial matrix can never produce a publishable package.

## CI

GitHub Actions throughout — unlike the rest of Papyrine, this repository has no AppVeyor pipeline. AppVeyor is Windows-only, and the managed layer here needs testing on Linux and macOS too.

Four workflows, each with a distinct job:

- **`.github/workflows/build.yml`** — builds and tests the managed side on Linux, Windows and macOS. Deliberately lets `dotnet build` drive cargo through `KrillaBuildNative` rather than invoking it directly, so the MSBuild wiring itself gets exercised on all three platforms. Uploads `*.received.*` on failure, without which a snapshot mismatch on a platform nobody has locally is close to undebuggable.
- **`.github/workflows/rust.yml`** — fmt, clippy (warnings are errors), tests, Miri, `cargo deny`, and a check that `THIRD-PARTY-NOTICES.md` is current. Runs on every push touching `rust/`. Also greps for `#[unsafe(no_mangle)]` outside `guard.rs`: a hand-written export would bypass the `catch_unwind` boundary and silently reintroduce process-abort-on-panic.
- **`.github/workflows/native.yml`** — the eight-RID cross-compilation matrix, `workflow_call` only. Each leg statically verifies its own output; see the header comment for why each check exists.
- **`.github/workflows/publish-nuget.yml`** — calls `native.yml`, packs all eight natives into one package, runs `IntegrationTests` against the real nupkg on four runners plus Alpine and Debian 12 containers, then publishes via nuget.org Trusted Publishing (OIDC). Needs the `NUGET_USER` variable and a trusted-publishing policy registered before the first tag.

`IntegrationTests/` consumes the *packed* package rather than a project reference, which is the only way to test that `runtimes/<rid>/native/` resolves at all. Its `nuget.config` pins `Krilla` to the local `../nugets` feed via `packageSourceMapping` — without that, NuGet sees the same version in both feeds and reliably picks nuget.org on CI, testing the last release instead of the current build.

## Licensing

The package is a binary distribution of ~94 statically linked crates, so their licences are an obligation on everyone who ships it. `rust/deny.toml` gates what may enter the tree; `rust/about.toml` + `about.hbs` generate `src/Krilla/THIRD-PARTY-NOTICES.md`, which is committed and CI-checked for drift.

Two `RUSTSEC` advisories are ignored in `deny.toml`, both *unmaintained* notices rather than vulnerabilities, on crates krilla chose (`ttf-parser`, `rustybuzz`) with no upgrade available. They are ignored individually rather than by disabling the advisories check, so a real vulnerability anywhere still fails the build.

## Architecture

### The native shim (`rust/crates/krilla-capi`)

- **`document.rs`** is the only module containing unsafe code, and the only one whose correctness rests on an argument rather than the type system. krilla models PDF construction as nested mutable borrows (`Document -> Page<'a> -> Surface<'a>`), which C cannot express, so one handle owns the whole chain with the lifetimes erased. Invariants I1–I4 are stated in the module header. **Verify changes here with Miri**, not review: Stacked Borrows violations are invisible to reading.
- **`guard.rs`** owns the ABI boundary. Every export goes through the `ffi!`/`ffi_doc!` macros, which wrap the body in `catch_unwind` and poison the document on a panic. `#[unsafe(no_mangle)]` appears *only* in this file's macro definitions; a hand-written export would silently reintroduce process-abort-on-panic.
- **`handle.rs`** carries the ownership rules R1–R6 that the rest of the crate's `// SAFETY:` comments cite by number.
- **`types.rs`** holds the `#[repr(C)]` structs, each with a `const` layout assertion. `api/` holds the exports, kept separate so the unsafe core stays small.

### Traps in krilla that the shim has to work around

These are not documented upstream and were found by reading the source. Do not remove the guards:

- **`Surface`'s `Drop` asserts** an empty push stack, no remaining sub-builders, and no open marked-content section. A panic inside `drop` during unwinding aborts the process regardless of `catch_unwind`, so `close_page` tracks push depth and tagged state itself and rebalances before dropping.
- **`Surface::pop`** unwraps an empty stack. `document.pop()` checks first.
- **`draw_glyphs`** slices the run text by byte range and panics off a UTF-8 character boundary. Validated in `api/text.rs`.
- **`Image::from_rgba8`** panics when the buffer length is not `width * height * 4`. Checked in `api/image.rs`.
- **Glyph advances must be pre-divided by units-per-em.** krilla does not check this and produces plausible but mis-spaced output. `Surface.DrawGlyphs` normalises, so the trap is unreachable from C#.

### The managed side (`src/Krilla`)

Mirrors Morph.PDFium: `KrillaNative.*.cs` partials of one `static partial class` in the global namespace, `[LibraryImport]` throughout, public types in the `Krilla` namespace, blittable mirrors in `Interop/`. The assembly sets `DisableRuntimeMarshalling`, so every ABI struct must stay plain-old-data.

`AbiTests` loops `krilla_abi_sizeof` over every mirrored struct. A layout disagreement between Rust and C# is the one bug class here that corrupts memory rather than throwing, so that test earns its keep.

### The HTML converter (`src/Krilla.Html`)

Three stages, each inspectable on its own: AngleSharp parses and runs the cascade, `Layout/` turns the styled tree into positioned boxes, and `Painting/PdfPainter` draws those boxes through `Krilla.Surface`. Implemented: block and inline layout, inline-block, tables, floats, relative and absolute positioning, the box model including `box-sizing`, collapsing margins, line breaking, alignment, pagination, images, links, and list markers. Flex and grid lay out as plain blocks — deliberately, so unimplemented CSS shows up in the corpus as a geometry difference rather than as missing content nothing measures.

Three structural points worth knowing before changing anything:

- **Layout needs the native, for shaping only.** `FontFace` reads its metrics out of the font bytes in managed code and `ImageData` reads image sizes from file headers, but `FontFace.Shape` calls through to krilla's rustybuzz. That crossing is unavoidable — measuring text correctly means shaping it — and it ended the earlier property that layout ran without a Rust toolchain.
- **Nothing applies its own vertical margins.** `BlockLayout.Layout` is handed the top of the border box; the caller consults `LeadingMargin` first. Margins collapse through nesting, so the margin above a box may have come from a grandchild, and only the ancestor placing it can know the final value.
- **An absolute box is positioned in a second pass.** `AbsoluteLayout` runs after flow, because an absolute box is measured against an ancestor that is sized by flowing the very children that may declare it. `LayoutBox.Positioned` holds them against the box that DECLARED them — which is what knows their static position, where flow would have put them and where they go when their offsets are auto — while the containing block is found by walking the ancestor chain in that second pass.
- **A float is out of flow but not out of the tree.** `LayoutBox.Floats` holds them, apart from `Children`, each with the index of the in-flow child it was declared before — a float starts at the flow position it was written at, not at the top of its container. Keeping them out of `Children` preserves the rule that a block container is all-block or all-inline, which a float inside a paragraph would otherwise break. `Descendants()`, `Translate` and the painter all still walk them.
- **Images do not fetch over the network**, and that is a security default rather than a missing feature — converting an untrusted document would otherwise issue requests to whatever hosts it names. `HtmlOptions.ImageResolver` is where a caller takes that decision explicitly, and `LocalImages`/`WebImages` (`ImagePolicy`) bound what any resolver may load. The policy is checked in `ImageStore.Resolve` *before* the resolver runs, which is the whole point: a caller-supplied resolver that fetches is the one with something to constrain, and a refused source is never requested. `data:` is ungated — its bytes are already in the document.
- **`OnDiagnostic` reports only the deliberate sites, never every unrecognised declaration.** The signal worth having is "recognised, and not rendered the way a browser would" — `display: flex`, a presentational attribute, an image that resolved to nothing. Reporting unknown CSS too would bury that under the `cursor` and `content` an ordinary stylesheet carries, and would cost the invariant its meaning: **a conversion that reports nothing laid out every construct in the document the way a browser would.** `UnsupportedCss` holds the table of what to report and `UnsupportedAttributes` the presentational attributes; both run only when `DocumentContext.Reports` is true, because the scan costs a cascade lookup per property per element and a caller who is not subscribed should not pay for it. Two rules keep the false-positive rate at zero, and both were arrived at by finding the false positives first: a value that is a **no-op** is silent (`float: none` in a reset stylesheet renders exactly as asked), and so is anything the **default stylesheet** supplies rather than the document — which is why `hr` is exempt from border-style reporting, since AngleSharp's built-in sheet makes every plain `<hr>` `inset`. Origin cannot be tested directly; `ComputeCascadedStyle` does not expose it, the same limitation `Inputs/flatten.css` works around.

### The corpus (`src/Krilla.Html.Tests/Inputs`)

One directory per scenario, holding `input.html`, `input.css`, `notes.md`, and the committed browser reference: `reference_0001.png` and `reference.boxes.json`. Structure borrowed from `Morph/src/Tests/Inputs`, which solved the same problem for DOCX.

Two shared stylesheets are prepended, and the split matters. `reset.css` is what makes the two engines **comparable at all** — it pins the fonts both sides load — and every scenario gets it. It used to disable kerning and ligatures too; shaping removed that concession, which is what lets the `text/` category measure them. `flatten.css` removes the default stylesheet so a scenario measures the layout algorithm rather than the defaults table, and a scenario opts out of it with a `no-flatten` marker file. The `ua/` category is exactly the scenarios that do opt out, because they exist to measure those defaults.

A generic font family cannot be pinned: the reference generator binds real family names through `@font-face`, and a generic is not legal as an `@font-face` family, so Chromium resolves `monospace` against whatever the host has installed. `reset.css` therefore names `Liberation Mono` on `pre`/`code`/`kbd`/`samp` directly. The consequence is that "does `<pre>` default to monospace" is a question this corpus cannot ask — its margins and white-space handling are still measured.

It records two independent measurements, and **asserts neither**:

- **`reference.boxes.json`** — the browser's `getBoundingClientRect()` per element, against our box tree. Integer-exact, localising ("this paragraph is 14px low" is a defect report), and — the practical reason it leads — computable without the native library, so it works on a machine with no Rust toolchain.
- **`reference_0001.png`** — pixels, via AbsoluteError and SSIM.

**Box geometry currently sits at zero across all 74 scenarios**, and 53 read SSIM 1.0000. Four of them got there by finding a defect first, which is the argument for adding a scenario for anything the engine implements rather than only for what it implements well: `block/anonymous` found trailing inline content hoisted above a block sibling, `position/fixed` found a fixed box resolved against the nearest positioned ancestor, `table/empty` found edge spacing on a section with no columns, and `page/table_break` found a break taken at the line inside a table row. Each is written up in its own `notes.md`. The remaining pixel residuals each have a named cause rather than a mystery: `ua/hr` (0.9911) is Chrome's `inset` rule painted solid, `text/kerning` (0.9945) and `text/ligatures` (0.9982) are sub-pixel glyph positioning, `image/inline_flow` (0.9998) is antialiasing on an image edge at a fractional position, and the rest are the same glyph positioning seen on fewer words. A scenario reading SSIM 1.0000 is not necessarily pixel-identical — seventeen differ on a scattering of antialiased pixels, which is what `AE` is there to show. Thirty-six are identical outright.

That the pixels go to *exactly* identical is worth understanding, because it is a consequence of a design choice and not a given. A Chromium **screenshot** would be rasterised by Skia and compared against a PDFium render of our PDF, and two rasterisers disagree about glyph edges no matter how correct the layout is — which would put a floor somewhere around 0.90–0.97 on any text-heavy page. Printing the reference instead means both sides are rasterised by PDFium, and that floor disappears entirely. **Do not switch the reference to a screenshot**; it would cost every exact match in the corpus and replace a hard signal with a fuzzy one.

Because the metrics are exact, a regression is unambiguous: anything below 1.0000 that was at 1.0000 is a real change, not noise.

Both land in `result.verified.json`, so a fidelity change shows up as a snapshot diff that has to be consciously accepted. `compare.md` per scenario and `compare-all.md` at the root are generated for reading side by side.

`reference_*.png` comes from Chromium's **printer**, not a screenshot. A full-page screenshot sliced into page-height strips would cut a line of text in half at every boundary, while both Chromium's printer and `Paginator` break between lines — so a sliced reference would report a difference at every page break that came from how the reference was made.

Regenerating is deliberate and separate (`Krilla.Html.RefGen`, `[Explicit]`, its own project). A reference regenerated during a test run would move the target to wherever the render landed, and the suite could never fail. Run it after adding a scenario or changing `Inputs/reset.css` — never to make a failing comparison pass. It needs Chromium once:

```bash
dotnet tool install --global Microsoft.Playwright.CLI && playwright install chromium
```

## Traps in AngleSharp.Css that Krilla.Html works around

Found by measuring against a browser, not by reading. Each cost a whole category of the corpus until it was understood.

- **AngleSharp's default stylesheet is the HTML 4.01 one**, not the modern rendering rules browsers implement. Ten of twelve common elements disagreed with Chrome: every heading below `h1` has the wrong margin, `h4` and `p` carry no font size, and `ul`/`ol` have no `padding-left` at all — so an unstyled document had no list indentation whatsoever. `UserAgentStyles.Corrections` is appended via `ICssDefaultStyleSheetProvider.AppendDefault`, which puts it in the **same user-agent origin** so author rules still beat it, and a later rule of equal specificity wins so it overrides rather than merely coexists. The provider is per-configuration, so appending per conversion does not accumulate.
- **`ComputeCurrentStyle` resolves percentages against the render device's viewport.** `width: 25%` inside a 600px container comes back as `204px` — a quarter of the *page*. A percentage resolves against a containing block, which is a layout result AngleSharp cannot know. So `StyleResolver` reads `IStyleCollection.ComputeCascadedStyle` instead, which leaves `25%` and `0.5em` as written, and `CssLength` carries them into layout to be resolved where the answer exists. Do not "simplify" this back to the computed style.
- **AngleSharp compares specificity across cascade origins.** CSS resolves origin *before* specificity, so an author `* { margin: 0 }` beats a UA `p { margin: 1.12em 0 }` outright and a browser zeroes it. AngleSharp lets the UA rule win on specificity, so `body` keeps its 8px margin and every paragraph keeps a 17.92px one. `Inputs/reset.css` enumerates element names to match the UA selectors' specificity. This is a real limitation for consumers too, not just a corpus quirk.
- **AngleSharp has no `display` for the inline elements.** It reports an empty string for `b`, `i`, `span` and the rest. Reading that as `block` puts every piece of emphasised text on a line of its own, which is a whole-paragraph error from a missing default. `Styling/UserAgentStyles.cs` supplies them.
- **AngleSharp does not resolve the `font-size` keywords**, so `medium`, `large`, `smaller` and the rest arrive at `ResolveFontSize` as written rather than as lengths. The trap is in what an unparseable value falls back to: `CssLength.Zero` is an **absolute** length, so it took the `LengthKind.Absolute` branch and returned a font size of 0 — which is not a smaller size, it is an invisible one, and `font-size: large` therefore deleted the text of the element carrying it. The fallback is `CssLength.None` so the `_` branch can catch it. Any new parse whose fallback is meant to mean "unparseable" needs a kind the switch above it does not otherwise handle. `<small>` and `<big>` never hit this, because the cascade resolves the default stylesheet's sizes for those into real lengths first — which is what kept it hidden.
- **AngleSharp does not apply presentational attributes.** HTML maps `<table width>`, `<td bgcolor>`, `<p align>` and the rest onto CSS as hints below every author rule; AngleSharp performs none of that mapping, so they reach the cascade as nothing at all. `<img width>`/`<img height>` are applied by hand in `BoxBuilder.WithAttributeSize`; everything else is reported by `UnsupportedAttributes` rather than silently dropped. Documents converted to PDF come disproportionately from reporting tools and mail merges, which emit exactly this markup.

## Traps found by importing an external test

`ua/acid1` is the CSS1 conformance test, imported from the W3C suite rather than written here. It is the only scenario in the corpus that nobody designed, and that is exactly what it is for: it found three defects on its first render that fifty-three hand-written scenarios had passed over, because each was invisible in the conditions those scenarios establish.

- **The root element's background propagates to the CANVAS**, covering the whole page rather than the root box alone (CSS 2.1 §14.2). When the root has none, `body`'s is taken instead and body then paints none — which is what makes `body { background: … }` colour a page. `Inputs/reset.css` paints the root white onto a page that was already white, so the corpus could never see this: Acid1 paints it blue and two thirds of the page was wrong.
- **`line-height` has to be inherited explicitly**, like every other inherited property, since the cascaded style carries no inherited values. It was the one that had been missed, so `body { line-height: 1.6 }` — how nearly every stylesheet sets line spacing — applied to `body` and to nothing inside it. A unitless value inherits AS THE NUMBER and is re-resolved against each descendant's own font size; inheriting the resolved pixels instead would give 32px text the spacing computed for its 16px ancestor.
- **`initial` is a no-op for every property in the `UnsupportedCss` table**, and reporting it is a false positive. It arrives far more often than authors write it, because a shorthand that omits a component sets that component to `initial` — `border: 0` produces a `border-style: initial` nobody typed. Not applied to `display` or `font-size`, whose initial values (`inline`, `medium`) this engine does not honour.

The lesson generalises past the three: a corpus written alongside an engine tests what its author already knows to doubt. One import, of markup written by someone else for a different purpose, reached three blind spots at once. `notes.md` records the two edits made to the source — the font family, and the removal of the form widgets the test itself exempts.

## Traps in floats

Every rule here was measured out of headless Chrome before it was written, because CSS 2.1 §9.5 states most of them loosely enough to admit more than one reading. `FloatGeometryTests` keeps the nineteen arrangements the rules were derived from; the `float/` corpus scenarios measure the same behaviour in pixels.

- **A float shortens LINE boxes, never block boxes.** A block in normal flow beside a float keeps its full width and simply overlaps it — only the lines inside it wrap. This is the single most surprising thing about floats and the easiest to get plausibly wrong: narrowing the block looks right on a page of prose and is wrong the moment the block has a background. `float/basic` measures it with `#block`.
- **Everything is measured to the MARGIN box.** Line shortening, float-against-float placement, and `clear` all use it. A float with `margin-right: 20px` holds text 20px further away, and `clear` past a float with a bottom margin lands below the margin, not the border. `FloatContext` stores margin boxes for this reason and nothing in it deals in border boxes.
- **The flow position is where the margin box goes, not the border box.** Adding the top margin when choosing the position AND again when translating the box into place puts a float with `margin-top: 10px` twenty pixels down. It cost a debugging round; `float/basic` would not have caught it, because its floats have no margins.
- **A line is shortened by the floats its box OVERLAPS, not by those under its top edge.** The two readings agree on every arrangement where floats begin and end on line boundaries, which is nearly all of them, and diverge when a float starts midway down a line. The discriminating case is the last in `FloatGeometryTests`: sampling at the top alone puts that line 150px too far left. The band is sampled over the strut height, since the real line height is not known until the line has been filled.
- **A box that establishes a formatting context grows to contain its floats; nothing else does.** In `float/basic` the root reaches 304px to enclose a float hanging out of a wrapper that ends at 260px, and the wrapper stays 260px. `BlockLayout` keys this off whether it created the `FloatContext` rather than off a separate flag, which makes the root, a float, and a table cell all behave correctly for the same reason.
- **A second float descends; it does not shrink and it does not overlap.** `FloatContext.Place` walks the candidate positions — the requested top, then each float bottom below it — and takes the first that fits. Handing each float the next free position along one axis passes the side-by-side case and fails the drop-below one, which is why `float/stacking` contains both.
- **Right floats stack right to left**, so the float written first in the source sits furthest right.
- **A float too wide to fit anywhere is placed at the top and allowed to overflow**, rather than descending forever looking for room that does not exist. The paragraph beside it then has no band at all, and its lines descend below it instead of being drawn in zero width.

## Traps in positioning

Measured out of Chrome before being written, the same as the float rules. `PositionGeometryTests` keeps the ten arrangements they were derived from; the `position/` corpus scenarios measure them in pixels.

- **The containing block is the ancestor's PADDING box.** Not its border box and not its content box. A parent with neither border nor padding cannot distinguish the three, which is why `position/absolute` gives its frame both — an absolute box at `top: 0; left: 0` lands inside the border and outside the padding.
- **A static ancestor in between contributes nothing.** Its position, margins and padding are skipped entirely on the way to the nearest positioned ancestor. Walking to the DOM parent instead is a plausible reading of "containing block" and is wrong by exactly that ancestor's offset, which is what `position/anchors` measures with a 30px margin.
- **A percentage offset resolves per axis**: `left` and `right` against the containing block's WIDTH, `top` and `bottom` against its HEIGHT. Unlike a percentage on `top` in relative positioning, which resolves against width like every other one. A square container hides the difference, so `position/anchors` deliberately uses one that is not square.
- **The available width for shrink-to-fit is what the OFFSETS leave**, not the whole containing block. CSS 2.1 §10.3.7 subtracts both offsets before the shrink-to-fit minimum, so a box at `left: 50%` wraps where the same content at `left: 0` does not. Passing the full width makes every offset box one line too short and as much too wide — caught by `position/anchors`, not by any probe case, because the probe cases had short text.
- **A FIXED box skips every ancestor, positioned or not.** Its containing block is the initial one, which in paged media is the page. That is the single thing distinguishing it from an absolute box, so `AbsoluteLayout` carries the initial containing block down alongside the accumulated one — treating the two alike is wrong by exactly the nearest positioned ancestor's offset, which is invisible whenever there is none. `position/fixed` is the scenario that has one.
- **`bottom` and `right` name the edge the box moves AWAY from.** In relative positioning `bottom: 5px` lifts a box; in absolute positioning it measures the gap from the containing block's bottom edge, so the box has to be sized before it can be placed.
- **Relative positioning is applied to the whole subtree at the end of `BlockLayout.Layout`**, after the height has been returned. It changes no measurement: siblings sit where they would have, the parent keeps its height, and the float context keeps the pre-offset entry for any float inside — which is also correct, since content outside flows as though the offset never happened.
- **Positioned boxes are painted from the root, not where they were declared.** They belong to the page's stacking layer rather than to their parent's flow position, and painting one inside its declaring parent buries it under any later sibling. That burial is the normal case rather than a corner one, because the box an absolute is anchored to is frequently an ancestor of the sibling that covers it. `PdfPainter.Hoisted` collects them in tree order — RELATIVE boxes too, which stay in flow and are still out of Appendix E's steps 3 and 7. It flattens the whole page into one list rather than nesting, which is Appendix E's own rule: a positioned descendant of a float, or of another positioned box, belongs to the parent stacking context.
- **Paint order is CSS 2.1 Appendix E, not document order**: in-flow blocks, then floats, then inline content, then everything positioned. It only becomes visible once boxes overlap, which is exactly what positioning is for — `position/relative` went from SSIM 0.9969 to 1.0000 on this change alone, with its geometry already exact.
- **And Appendix E is a set of PHASES over the whole page, not an order within each box.** Every background and border in a layer goes down in tree order, then every float, then every line — so an earlier sibling's overflowing text sits on top of a later sibling's background rather than under it. Applying the same sequence box by box is indistinguishable while nothing overlaps, which is why it survived so long: making the phases global left all 69 existing scenarios pixel-identical and changed only the order of the operators in their PDFs. `block/overflow_paint` is the case that tells them apart, with a box overflowed by `max-height` and a float taller than the box that declared it.
- **A block-level replaced element's content paints with the inline content, not with the backgrounds.** Appendix E step 7 rather than step 3: an image hanging out of a short box sits over a later sibling for the same reason that box's text does.

## Traps in text layout

Three of these were found the same way: a scenario sat at SSIM ~0.93 while its box geometry was exact, meaning layout was right and painting was wrong. That combination is the signature of a rasterisation-level bug, and it is worth recognising.

- **Half-leading must be FLOORED, and the descent derived by subtraction.** With integer ascent and descent the exact half lands on .5 constantly — 16px text in a 24px line gives 3.5 — and a baseline at 17.5 rasterises one whole pixel lower than one at 17. Every glyph in the corpus was a pixel low until this was floored. `Below` is then `lineHeight - Above` rather than a second floor, or each line loses a pixel and the drift compounds into a wrong page count.
- **`line-height: normal` needs the font's metrics rounded to whole pixels *before* summing.** Liberation Sans at 16px gives 14.48 + 3.39 + 0.52, which is 18.4 unrounded but 14 + 3 + 1 = 18 the way a browser does it. Four tenths of a pixel per line is invisible on one line and a whole line of drift down a page. This is the one place the engine imitates a specific implementation rather than a specification — CSS defines `normal` as UA-defined, so there is no correct value to compute and agreeing with the reference browser is the useful choice.
- **A page break can be needed where no line offers one.** `Paginator` breaks before a line that would straddle the boundary, but a block taller than the page contains no lines at all — so there is no candidate anywhere inside it, the break lands after the whole block, and everything between the page edge and the block's end is never drawn. `NextTop` falls back to breaking at the page edge for exactly that case.
- **A page ends where the next one begins, not at the bottom of the paper.** `Paginator` moves a straddling line whole to the next page, so the last line on a page can end well short of the sheet. `PdfPainter.Paint` therefore takes `pageEnd` separately from the page box: painting down to the paper instead draws that line here clipped in half AND again overleaf in full.
- **A table ROW is unbreakable, the way a line is everywhere else.** Breaking at a line inside a cell lands the break below the cell's top padding, so the row resumes overleaf missing it and everything after it on the page sits high by exactly that. `Paginator.Unbreakable` yields a row's border box in place of the lines inside it.
- **A box moved WHOLE to the next page is not the same as one the break falls inside, and the painter must not treat them alike.** The moved box paints nothing here — `PdfPainter.PaintBox` culls on its top edge — while the box the break falls inside is fragmented, and a browser fills the rest of the page with that fragment. Clipping the page at `pageEnd` handles the first and breaks the second: `page/multi_page_flow` keeps its paragraph background to the paper's edge with a line moved overleaf, and lost 1.6% of its pixels to a clip before the two cases were separated.
- **Leading white space is only trimmed where white space collapses.** Under `pre` the indentation *is* leading spaces, and applying the `normal` trimming rule left-aligns every deliberately indented line.
- **`text-indent` NARROWS the first line's band; it does not shift it.** Shifting is indistinguishable on a left-aligned line and wrong on a centred one, which is centred in what the indent leaves. It is applied by the block that generates the line rather than by the one carrying the declaration, because the property inherits — applying it where it is declared makes `body { text-indent: 2em }` indent the first paragraph and no other. `IntrinsicWidths` adds it too, or a shrink-to-fit box is sized with no room for it and wraps a line that was meant to fit.

## Traps around links

- **A link annotation is NOT painted through the transform stack.** krilla queues annotations and applies them when the page closes, so they never see the scale and translate the rest of the page is drawn through. `PdfPainter` converts to page points itself, and that conversion is the only place two coordinate spaces coexist on one page.
- **One annotation per line fragment, not one per anchor.** A PDF link is a rectangle, so an anchor that wraps needs one per line — a single box round the lot would make the blank space at each line end clickable, and on a short line would cover text that is not part of the link.
- **Fragment links resolve after pagination**, because a fragment names an element while a PDF internal link names a page and a point on it. `LinkTargets` is built once the page tops are known.
- **An unresolved fragment produces no annotation at all**, rather than one aimed at page one. A link that silently goes somewhere wrong is worse than a link that is not there.
- **Neither corpus measurement can see a link.** An annotation carries no appearance stream, so the pixel comparison is blind to it, and it is not an element box, so the geometry comparison is too. `CorpusRunner` reads them back out of the produced PDF and snapshots them — and the box and pixel numbers staying at zero alongside is the separate check that adding links disturbed no layout.

## Traps around inline-block

The second atomic inline, and everything awkward about it comes from having a box tree where an
image has a rectangle. `inline/inline_block` and `inline/inline_block_sizing` measure the rules
below; both are exact against Chrome.

- **Its baseline is its LAST in-flow line's, not its first** (CSS 2.1 §10.8.1). A two-line
  inline-block hangs UPWARD so the text beside it lines up with its second line, which is the
  opposite of what reading "aligns on the baseline" suggests. Taking the first line's puts the box
  a whole line too low, and looks entirely plausible on the one-line case that is most of them.
- **With no in-flow line box it falls back to the bottom MARGIN edge** — which is exactly an
  image's rule, reached from the other direction. An empty one, or one holding nothing but an
  image, therefore behaves like the atomic inline the engine already had.
- **It is measured to its MARGIN box on the line**, the same rule floats follow. `Token.Width` is
  the margin box, so a row of them wraps on the margins rather than the borders.
- **It is laid out in `InlineLayout`, not `BlockLayout`.** A line cannot be filled until the box's
  size is known, and its size is a whole layout of its own — including a fresh `FloatContext`,
  since it establishes its own block formatting context and so contains its floats.
- **It is always given an ASSIGNED width, never the ordinary horizontal resolution.** That path
  reads `margin: auto` as a request to centre, which is what it means for a block in flow and not
  what it means here: an auto margin on an inline-block is zero.
- **`Intrinsic` must not lay it out.** The intrinsic pass runs to decide a column's width, so
  sizing the box against a width nobody has settled is both wasted and wrong — hence `Tokenize`'s
  `measuring` flag, and `Token.MinWidth` alongside `Token.Width`, since a box already sized to the
  room available cannot report how far it could be squeezed.
- **It hangs off the LINE, not off `Children`** (`LineBox.Boxes`), which keeps the rule that a
  block container is all-block or all-inline — the same reason `LayoutBox.Floats` is a list of its
  own. Everything walking the tree has to reach it through there: `Descendants`, the painter,
  `AbsoluteLayout` and `PdfPainter.Hoisted` all do, and each was a separate line of code. Miss one
  and an absolute box inside an inline-block is never placed, or a fragment link into one resolves
  to nothing.

## Traps around replaced elements

- **`<img>` is inline-level.** Defaulting it to block puts a picture on a line of its own, so an image mid-sentence drops below the paragraph text. It lives in `UserAgentStyles`' inline set alongside `<b>` and `<span>`, despite being replaced rather than textual.
- **A replaced box is never self-collapsing.** `IsSelfCollapsing` tests for a zero height, and an image sized from its aspect ratio has `height: auto`, which reads as zero — so without an explicit exclusion the image's own bottom margin collapses through it and pushes it down by that margin.
- **An atomic inline sits its bottom edge on the baseline**, so a tall image pushes the line's top upward rather than growing it downward. That is what `vertical-align: baseline` means for a replaced element, and it is why an image taller than the line still leaves the text where it was.
- **A percentage width resolves against the CONTAINER, not against what is left of it.** `width: 50%` on an image in a 600px block is 300px of picture whatever padding the image carries — taking the image's own padding and border off the 600 first makes a padded image narrower than an unpadded one asking for the same share. Measured; `image/percent_width` keeps it, in both box-sizing modes, and its heights are what check that the aspect ratio is applied to the content box on both.
- **The vertical surround is its own quantity, not the horizontal one.** `box-sizing: border-box` takes the padding and border out of a declared `height` as well as a declared `width`, and the two pairs differ the moment the padding is not uniform — so deflating a height by the horizontal pair feeds a wrong number into the aspect ratio and the WIDTH comes out wrong too, on the axis nobody declared. `image/percent_width`'s `#ratio` is 250x120 against the 170x120 that mistake produces.
- **Clamping a width has to rescale an auto height.** `max-width: 100%` on a photograph in a narrow container must shrink both dimensions; rescaling only the width is how images end up distorted in responsive layouts. `ReplacedSizing` does it, and `image/max_width` exists to catch a regression.
- **Corpus options are per scenario, not shared.** `CorpusRunner.Options(directory)` sets the base URL a relative `src` resolves against. Calling the parameterless overload for a scenario silently drops every image and reports the absence as a layout difference — which is exactly how a wrong `BoxFidelityTests` baseline nearly got promoted.
- **`OS/2` bit 7 (`USE_TYPO_METRICS`) changes which vertical metrics win.** When set, `sTypo*` beats `hhea`. Browsers honour it, so `OpenTypeMetrics` does too; ignoring it puts every line box a few percent out, which compounds into a wrong page count.
- **Text is shaped, not summed.** `krilla_font_shape` exposes the rustybuzz krilla already links, so advances carry kerning and ligatures. Reusing krilla's shaper rather than adding a second one is the point: two shapers would eventually disagree, and a measurement that disagrees with the drawing is worse than no measurement. `ShapedText` shapes each inline item once and answers sub-range questions by summing known advances — shaping per candidate substring instead would make laying out a paragraph quadratic in its length.
- **`krilla_font_new` keeps the font bytes.** krilla's `Font` will not give them back (`font_data`, `index` and `variation_coordinates` are all `pub(crate)`), and shaping needs them. The same `Arc` goes to krilla, so this shares one allocation rather than holding a second copy of every font file.
- **A glyph run is freed by `krilla_glyphs_free`, not `krilla_buffer_free`.** A glyph is 40 bytes with 8-byte alignment, so returning it through the `u8` path would hand the allocator the wrong layout.
- **Whitespace-only inline content generates no box.** The newline between two block elements is collapsible whitespace, and wrapping it in an anonymous block gives every indented document a blank line before each section.
- **An anonymous block is one per contiguous RUN, not one per container.** `BoxBuilder.CloseRun` closes a run at a block-level sibling and appends it, so the children stay in source order. Gathering every run into a single block and putting it first is cheaper and is right until a container has text AFTER a block child, at which point the document is reordered — which is what `block/anonymous` measures. A float or an absolute box does NOT close a run: it is out of flow, so the text either side of it is one paragraph flowing around it, and that is also what keeps the child index each out-of-flow box recorded correct without any shifting afterwards.

## Traps around list markers

There is no specification for any of this. CSS says a marker is placed "outside the principal box" and leaves every offset to the user agent, so — as with `line-height: normal` — there is no correct value to compute and agreeing with the reference browser is the only useful target. Every number in `ListMarkers` was measured out of headless Chromium across seventeen font sizes and three families, and each reproduces Chrome exactly at all of them.

- **The arithmetic is integer, and the truncation is the point.** A symbol's side is `(ascent * 2 / 3 + 1) / 2` and its top is `ascent - 3 * (ascent - ascent * 2 / 3) / 2` above the baseline, both in whole pixels off the whole-pixel ascent. No rounded float expression reproduces the uneven steps this produces — 14px and 15px text share a four-pixel bullet, then 15px and 16px share a five-pixel one.
- **Chrome's marker geometry is device-scale dependent.** The same page at a device scale factor of 8 puts its bullets somewhere else entirely, because the whole-pixel rounding happens in device pixels. Probe it at one device pixel per CSS pixel or the numbers you measure will not be the ones the corpus needs.
- **A marker hangs off the item's BORDER edge, not its content edge.** So `padding-left` on an `<li>` indents its text and leaves the bullet where it was. Measured, and not what a reading of "outside the principal box" suggests.
- **A marker sits on the item's first line, but is sized by the item's own font.** The two come from different places: the baseline is a layout result and may be several blocks down and below a margin that collapsed through, while the size follows the `<li>`'s own ascent. An item whose only child is 32px text still gets the bullet its own 16px style asks for. That is why `ListMarkers.Place` runs at the END of `BlockLayout.Layout`, after the subtree.
- **A counter marker's text is `N. ` — with the trailing space — right-aligned so the END of the advance lands on the edge.** Dropping the space moves every number four and a half pixels right at 16px. It is shaped through `ShapedText` like any other run, so the glyphs painted are the ones measured.
- **`circle` must be STROKED, not filled as a ring of two contours.** Both give the same nominal shape, but the corpus reference is Chrome's PDF rasterised by PDFium, so constructing the shape the way Chrome does is what makes the pixels agree — an annulus left a visible thickness difference along the top and bottom arcs, and stroking removed it. This is the general lesson the corpus keeps teaching: matching the browser's construction beats matching its description.
- **A uniform border must be one ring, not four mitred trapezia.** Two antialiased edges meeting on a mitre diagonal do not composite to full coverage, so every corner pixel comes out part transparent — about six pixels per corner, measured. Browsers have the same special case for the same reason. `PaintBorders` mitres only when the four edges do not share a colour, which is the only time the diagonal is visible anyway.

## Traps in tables

Like list markers, almost none of this is specified in usable detail. CSS 2.1 §17.5.2 describes the automatic column algorithm as a sketch and explicitly leaves the distribution to the user agent, so `TableLayout`'s numbers were measured out of Chrome across thirty-two constructed cases rather than derived. They reproduce it to within a hundredth of a pixel on every one.

- **The column algorithm is two rules, not one, and which applies depends on the table's width.** Above its max-content width, each column gets a share proportional to its max-content width. Between min-content and max-content, each column gets its min-content width plus a share of the slack proportional to how much it could grow. Using either rule alone is visibly wrong in the other regime, and both look plausible in isolation.
- **A colspan shortfall is shared proportionally; a rowspan shortfall is shared equally.** Opposite rules for what looks like the same problem, and both were measured. Sharing a wide spanning cell's extra width equally puts it into columns that had almost nothing in them.
- **A declared width on a table is a BORDER-box width, and that is a user-agent RULE rather than a rule of table layout.** `UserAgentStyles.Corrections` gives tables `box-sizing: border-box`, so `width: 300px` with a 10px border leaves 280 for content — and an author who writes `box-sizing: content-box` on a table gets 320, which is what Chrome does and what proves the rule is where it is. `TableLayout.ResolveWidth` therefore goes through `box-sizing` like every other declared width instead of subtracting the border unconditionally. It is also a minimum rather than an instruction — a table never renders narrower than its columns' min-content widths, whatever the declaration says.
- **A percentage column width means different things to the two algorithms.** Automatic layout treats it as the whole column, border and padding included, because it competes with content widths that are measured that way. Fixed layout adds the padding on top, because there is no content to compete with and the percentage is the cell's own `width`. The difference is exactly the cell's padding, which is small enough to look like a rounding error.
- **A table is never narrower than its caption's longest word** — the caption's min-content width, not its maximum. Two captions sharing a longest word produce tables of exactly the same width however different their lengths.
- **A cell's content is centred, and it is the CONTENT that is centred rather than the cell's height.** The user-agent sheet puts `vertical-align: middle` on the table and `inherit` on the cells, so the default is middle rather than the `baseline` the property's initial value suggests. And a cell with `height: 100px` holding one line is a hundred pixels tall with eighteen pixels of content: centring the used height leaves the text against the top edge, looking exactly as though vertical alignment were not implemented.
- **A shrink-to-fit table lands on the proportional branch with nothing to spare**, so the multiply-then-divide round trip loses a hundredth of a pixel — enough for the last word in the widest cell to stop fitting, which wraps it and makes the table a whole line taller. `Distribute` clamps each column to its own maximum, which the arithmetic guarantees and floating point does not.
- **An empty table occupies nothing**, not two pixels square. With no columns there is nothing for the edge spacing to be outside of.
- **A table lays out its children by ROLE rather than in order**, so a child with no table role is not merely misplaced — it is never positioned or painted at all. Unreachable from HTML, whose parser moves stray content out of tables, and reachable from `display: table` in a stylesheet. `BoxBuilder.TableFixup` wraps such children in anonymous rows and cells; its geometry is not measured against a browser, and content being on the page at all is the point.

## Traps in box-sizing

`block/box_sizing` and `image/percent_width` measure these, both exact against Chrome.

- **It applies to all six of `width`, `height` and the two min/max pairs**, not to `width` and `height` alone. So `max-width: 400px` under `border-box` caps the BORDER box, and the clamp has to happen before the auto margins are resolved — otherwise `max-width` with `margin: 0 auto`, the standard centring idiom, centres a box that is padding-and-border wider than it asked to be.
- **A declared size narrower than the box's own padding and border floors the content at zero**, leaving the box exactly as wide as its surround. Not negative, and not the declared value either: `width: 30px` with 50px of surround is a 50px box, which is what Chrome gives.
- **Eight sites turn a declared width or height into a used one, and only two of them are in `BlockLayout`.** A float's shrink-to-fit, an inline-block's assigned width, a table's own width, a table column's fixed width, the intrinsic pass and replaced sizing each resolve one themselves. `ComputedStyle.ContentSize` exists so that the property is applied in one place and a new site is a one-line call rather than a rediscovery; adding a site that skips it fails nothing, which is the usual problem.
- **The corpus's `flatten.css` has to restate `table { box-sizing: content-box }`** even though its `*` rule already says so. This is the AngleSharp origin trap in its purest form: in a browser the author `*` rule beats the user-agent `table` rule outright, and here the UA rule wins on specificity — so without the restatement a table with both a width and a border would be laid out border-box on one side and content-box on the other, and the difference would read as a table-layout defect.

## The diagnostic table is only as good as its audit

`UnsupportedCss` reports what the engine reads and does not honour, and the invariant it carries — **a conversion that reports nothing laid out every construct in the document the way a browser would** — is false the moment a property is neither read nor listed. Five were found that way, by diffing what `StyleResolver` reads against what the table lists rather than by anything failing: `min-height`, `max-height` and `text-indent` were implemented in response, and `box-shadow` and `caption-side` were added to the table. Re-run that audit when adding properties; nothing fails on its own if an entry is missed, which is exactly the problem.

Known and still unaudited: `vertical-align` on an inline box (only the table-cell case is honoured or reported, and `sub`/`sup` take theirs from the default stylesheet, so a report would need the exemption `hr` has), `outline`, `object-fit`, `hyphens`, and a tab under `pre`, which reaches the shaper as a character rather than advancing to a tab stop.

## Things that will surprise you

- **The RID table is duplicated in three places** and all three must stay in sync: `src/Krilla.Native.props` (the MSBuild table), `.github/workflows/native.yml` (the build matrix), and `readme.md` (documented support).
- **Every allocation crossing the ABI is freed by the side that made it.** The Windows natives build with `+crt-static` — required, or the DLL imports `VCRUNTIME140.dll`, which is not present on a clean Windows install — and that gives the library its own heap. Freeing a Rust allocation from managed code corrupts it.
- **`.editorconfig` at the repo root is generated**, overwritten by ProjectDefaults on every build. Never hand-edit it. `rust/.editorconfig` sets `root = true` to keep it out of the Rust tree.
- **`Krilla.Native.targets` must never be packed** into `build/` or `buildTransitive/`. It shells out to cargo, and packing it would make every consumer's restore require rustup.
- **`%(Identity)` is illegal in a project-scope ItemGroup condition** (MSB4190), which is why `KrillaResolveHostNative` is a target rather than plain evaluation.
- **`build.yml` produces a host-RID-only package** and must never publish it. The publishable one comes from `publish-nuget.yml`, built from the full eight-RID matrix.
- **SBOM under-reports.** `Microsoft.Sbom.Targets` sees no dependencies while ~110 Rust crates are statically linked; a separate Rust SBOM ships alongside.
- **`CorpusLayout.cs` is compiled into two projects**, via a linked `<Compile>` in `Krilla.Html.RefGen.csproj`. It holds the page size and DPI that the browser producing a reference and the test comparing against it must agree on exactly. A disagreement does not fail loudly — it silently suppresses SSIM and skews the error metric, because the two images stop being the same size. It locates itself with `[CallerFilePath]` rather than ProjectDefaults' generated `ProjectFiles`, which would point at RefGen's directory in one of the two.
- **The Liberation fonts in `src/Krilla.Html.Tests/Fonts` are load-bearing, not fixtures.** krilla has no font database, so both the converter and the reference browser load these exact files; without that, text reflows between machines and every recorded metric becomes noise. They are metric-compatible with Arial, which also means many glyphs have *identical* advances in regular and bold — a single-character width comparison between weights will fail against a perfectly correct font set.
- **A corpus scenario needs `Krilla.Html.RefGen` run before it measures anything.** Without a reference it still runs and still snapshots, but both comparisons are null, so it looks like a passing test while measuring nothing. `BaselineHealthTests.ScenariosHaveReferences` is what stops one being added and forgotten.
- **`mdsnippets.json` excludes `target`, and the exclusion is load-bearing.** Two projects now reference MarkdownSnippets, their scans run concurrently during a solution build, and both open `rust/target/.cargo-artifact-lock` — which fails the build with "another process has locked a portion of the file". It only reproduces once the native has been built, so a clean checkout will not show it and removing the exclusion looks harmless.
- **`BaselineHealthTests`' degeneracy threshold is 1, not Morph's 16.** Morph reasons that a rendered page always carries anti-aliased text and so has hundreds of colours. This corpus deliberately contains flat-fill scenarios with three colours total, precisely so they carry no rasterisation noise — anything above two fails them. The guard is correspondingly narrower, which it can afford to be because every page here is also compared against a browser reference.

## Package Management

Central Package Management; versions live in `src/Directory.Packages.props`. Rust dependencies are pinned exactly (`=0.8.2`) because the test suite compares PDF bytes, so a patch bump is a deliberate, baseline-regenerating change.
