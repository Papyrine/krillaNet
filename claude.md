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

# One scenario or one category, via an environment variable. The measure-first workflow stands up a
# throwaway scenario to probe the browser for a number, and regenerating the other hundred to read
# it back is most of a minute per probe. A name matching nothing is an error, not an empty run.
KRILLA_REFGEN=block/calc,text/ dotnet run --project src/Krilla.Html.RefGen -- --treenode-filter "/*/*/ReferenceGenerator/*"

# Benchmarks. Release only — BenchmarkDotNet refuses to run against a debug build — and a filter
# is required, since with none it prompts for one interactively.
dotnet run --project src/Krilla.Html.Benchmarks --configuration Release -- --filter "*"

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

Three stages, each inspectable on its own: AngleSharp parses and runs the cascade, `Layout/` turns the styled tree into positioned boxes, and `Painting/PdfPainter` draws those boxes through `Krilla.Surface`. Implemented: block and inline layout, inline-block, tables, floats, relative and absolute positioning, the box model including `box-sizing`, collapsing margins, line breaking, alignment, pagination including forced page breaks, `overflow` clipping and the block formatting context that comes with it, `visibility`, `text-transform`, letter and word spacing, the `font-size` keywords, all three text decorations, `vertical-align` on inline boxes, dashed, dotted and double borders, `border-radius`, `opacity`, `transform`, `z-index` and the stacking contexts that come with it, linear and
radial gradients, `outline`, `object-fit`, `caption-side`, `list-style-position`, both border models including `border-collapse`, raster and SVG images, links, and list markers. Flex and grid lay out as plain blocks — deliberately, so unimplemented CSS shows up in the corpus as a geometry difference rather than as missing content nothing measures.

And, from the paged-media and structure work: a document's `@page` rules decide the paper unless
`HtmlOptions.HonourPageRules` says otherwise, media queries resolve against PRINT, sided forced
breaks insert the blank page they ask for, generated content works (`::before`, `::after`, `content`
with `attr()`, `counter()`, `counters()`, `url()` and the quote keywords, plus `counter-reset`,
`counter-increment` and `quotes`), and the PDF gets an outline from the document's headings, a named
destination per `id`, and its title and language from the document. Content repeats per page:
`@page`'s sixteen margin boxes carry running headers, footers and page numbers, a `position: fixed`
box is drawn on every page, and a table's `<thead>` is re-drawn at the top of every page the table
continues onto. `@page` page selectors work — `:first`, `:left`, `:right` and `:blank`. `orphans`
and `widows` are implemented and OFF by default — see the trap list.

Also implemented, and each measured against Chrome the same way: `calc()` and the viewport units,
`vertical-align` given a length or a percentage, the inline box model (background, padding, border
and horizontal margins, one fragment per line), raster background images with `background-repeat`,
`-position`, `-size`, `-clip` and `-origin`, soft hyphens, `overflow-wrap`/`word-break`, tabs under
`pre`, `text-decoration-color` and `-style`, `object-position`, `empty-cells`,
`list-style-image`, `aspect-ratio`, `text-shadow` and `box-shadow` (offsets only),
`text-decoration-thickness` and `text-underline-offset`, `rgba()` on `color` and `background-color`,
and `<col>`/`<colgroup>` widths.

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

**Box geometry currently sits at zero across all 122 scenarios, with nothing unmatched**, and 88
read SSIM 1.0000. Several got there by finding a defect first, which is the argument for adding a
scenario for anything the engine implements rather than only for what it implements well:
`block/anonymous` found trailing inline content hoisted above a block sibling, `position/fixed`
found a fixed box resolved against the nearest positioned ancestor, `table/empty` found edge spacing
on a section with no columns, `page/table_break` found a break taken at the line inside a table row,
`inline/vertical_align_length` found that the corpus could not see an inline element at all, and
`text/word_break` found a word moved to a fresh line and then never offered for splitting, and
`position/z_index` found an absolute box inside a stacking context painted twice — once where it
belonged and once flattened onto the page. Each is written up in its own `notes.md`. The remaining pixel residuals each have a named cause rather than
a mystery: `block/border_styles` (0.9906) and `ua/hr` (0.9911) are patterned and `inset` rules,
`text/kerning` (0.9945) and `text/ligatures` (0.9982) are sub-pixel glyph positioning,
`text/decoration_style` (0.9999) is `text-decoration-skip-ink`, `image/inline_flow` (0.9998) is
antialiasing on an image edge at a fractional position, and the rest are the same glyph positioning
seen on fewer words. A scenario reading SSIM 1.0000 is not necessarily pixel-identical —
twenty-eight differ on a scattering of antialiased pixels, which is what `AE` is there to show.
Sixty are identical outright.

**The unmatched count is an assertion, not a statistic.** `BaselineHealthTests.EveryElementIsMeasured`
requires every element the browser laid out to have a box on this side. It closes the same hole
`ScenariosHaveReferences` closes one level up: a scenario with a reference still measures nothing
about an element this engine generates no box for, and the comparison counts it unmatched and
carries on green. Inline elements, inline images, inline-blocks and `<br>` all report their geometry
now, so the count really is zero and anything putting an element out of reach again has to be a
deliberate edit to that test.

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
- **AngleSharp rewrites a gradient's corner keyword as a flat `45deg`.** `to top right` names an angle that depends on the box's proportions — the gradient line is perpendicular to the diagonal joining the other two corners, so in a wide, short box it is nearly `to top`. The cascade collapses it to 45° before the engine sees it, which is right only for a square box. It cannot be reported either, being indistinguishable from an angle the author wrote. `GradientPaint.Resolve` keeps the correct resolution against the day the value survives, and `block/gradients` had the row that measured it removed for this reason.
- **AngleSharp DROPS some declarations rather than passing the value through**, which is a different failure from mis-resolving one and a worse one to debug: the cascaded style comes back *empty*, indistinguishable from a property nobody declared. So the value can be neither honoured nor reported, and the gap is invisible from this side. Found so far: the `revert` keyword, `text-overflow`, the `min-content`/`max-content`/`fit-content` sizing keywords, `recto` and `verso` on both break spellings, `aspect-ratio` given a single number rather than a ratio, `overflow-wrap: anywhere`, `content` given a `string()`, and the whole of `@page` except its margins — its `size`, its selector, and its margin box at-rules, which have no object at all. `unset` survives, and `calc()` and the viewport units survive verbatim — so the rule is not "anything modern". The ones worth working around were `@page`'s, recovered from the stylesheet's own text because a page size is a whole-document difference and a running header is the reason most documents have the rule; the rest are recorded and left.
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
- **A FIXED box is drawn on EVERY page, and that is a painting rule rather than a layout one.** CSS 2.1 §9.6.1 says so and Chromium's printer agrees, which `page/fixed_repeat` measures across three sheets. It costs one transform: everything on a page is painted through a translate that has already subtracted the page's top, so adding the top back puts the box at the same place on every sheet. The corpus can only see it in PIXELS — the box harvest runs against one continuous layout, so the browser reports such a box exactly once and an engine drawing it on page one alone matches the geometry comparison perfectly.
- **A fixed box with `top` and `bottom` both auto is the exception, and is painted once.** Its position is its STATIC position, which is a position in the DOCUMENT rather than on a page — so repeating it would add each page's own top to a coordinate that already includes it, and a box whose flow position is on page three would fall off the bottom of every page and vanish. Chromium does something else again: it draws such a box where flow put it, across a page boundary if that is where it falls, and ALSO repeats it at that page-relative offset on every later page, so a straddling box appears twice on the page after it. Measured, deliberately not matched, and reported by `UnsupportedCss.Fixed` — which is why that entry narrowed rather than disappearing.
- **`position: fixed` establishes a stacking context**, which CSS Position 3 says outright and which is also what holds the subtree together here: anything flattened out of it onto the page would be painted without the per-page translate, at its page-one position, on every page.
- **`bottom` and `right` name the edge the box moves AWAY from.** In relative positioning `bottom: 5px` lifts a box; in absolute positioning it measures the gap from the containing block's bottom edge, so the box has to be sized before it can be placed.
- **Relative positioning is applied to the whole subtree at the end of `BlockLayout.Layout`**, after the height has been returned. It changes no measurement: siblings sit where they would have, the parent keeps its height, and the float context keeps the pre-offset entry for any float inside — which is also correct, since content outside flows as though the offset never happened.
- **Positioned boxes are painted from the root, not where they were declared.** They belong to the page's stacking layer rather than to their parent's flow position, and painting one inside its declaring parent buries it under any later sibling. That burial is the normal case rather than a corner one, because the box an absolute is anchored to is frequently an ancestor of the sibling that covers it. `PdfPainter.Hoisted` collects them in tree order — RELATIVE boxes too, which stay in flow and are still out of Appendix E's steps 3 and 7. It flattens the whole page into one list rather than nesting, which is Appendix E's own rule: a positioned descendant of a float, or of another positioned box, belongs to the parent stacking context.
- **Paint order is CSS 2.1 Appendix E, not document order**: in-flow blocks, then floats, then inline content, then everything positioned. It only becomes visible once boxes overlap, which is exactly what positioning is for — `position/relative` went from SSIM 0.9969 to 1.0000 on this change alone, with its geometry already exact.
- **And Appendix E is a set of PHASES over the whole page, not an order within each box.** Every background and border in a layer goes down in tree order, then every float, then every line — so an earlier sibling's overflowing text sits on top of a later sibling's background rather than under it. Applying the same sequence box by box is indistinguishable while nothing overlaps, which is why it survived so long: making the phases global left all 69 existing scenarios pixel-identical and changed only the order of the operators in their PDFs. `block/overflow_paint` is the case that tells them apart, with a box overflowed by `max-height` and a float taller than the box that declared it.
- **A block-level replaced element's content paints with the inline content, not with the backgrounds.** Appendix E step 7 rather than step 3: an image hanging out of a short box sits over a later sibling for the same reason that box's text does.

## Traps in stacking order

`position/z_index` measures these, and is both pixel-identical and geometry-exact against Chrome.
Nothing here moves a box, so the geometry staying at zero is what says the property changed painting
alone.

- **`z-index` was READ NOWHERE and was absent from the diagnostic table**, so a document stacking its
  boxes deliberately came out in tree order in silence. Found by diffing what `StyleResolver` reads
  against what `UnsupportedCss` lists — the same audit that found `min-height` and `list-style-type`,
  and the same shape: a considered-looking comment (`// z-index is not implemented.`) beside the
  fallback, which is exactly where an unreported gap hides.
- **`z-index: 0` is not the no-op it reads as.** Any integer on a positioned box — zero included —
  establishes a stacking context and confines the positioned boxes inside it; only `auto` does not.
  So `ZIndex` is `int?` rather than `int`, and null is a different thing from zero at both of the
  places it is read. `position/z_index`'s `#zero` row is the only place the corpus can see it.
- **A level is measured against SIBLINGS, not against the page.** A descendant at `z-index: 100`
  inside a context at 1 stays under a sibling context at 2. Comparing levels globally is the
  plausible reading and puts the descendant on top.
- **A negative context paints between a box's own background and its content**, which is what makes
  `z-index: -1` disappear behind the background of the very box that declares it. Those two were one
  walk — `Backgrounds` painted a box's decoration and then descended — so honouring it meant lifting
  Appendix E step 1 out of the layer walk into `PaintStack`. That lift is the whole structural change
  the property cost; the sort was the easy half.
- **The sort has to be STABLE.** `auto` and `0` share step 6 and fall back to tree order, which is
  the order `Hoisted` already yields. An unstable sort is correct on every arrangement where no two
  boxes share a level, which is nearly all of them — hence the `#tie` row, which renders identically
  with or without the property implemented and exists only to pin this.
- **`Hoisted` guarded its CHILD walk against descending into a context and not its `Positioned`
  one** — and an absolute box hangs off the box that declared it, so `Positioned` is the branch
  nearly every one of them arrives through. Everything positioned inside a context was collected
  twice: once inside it, and once flattened onto the page where its level was compared against the
  page's. Invisible before this, because the only contexts were `opacity` and `transform` boxes and
  no scenario nested an absolute inside one. `position/z_index`'s last two rows found it on their
  first render.
- **An unpositioned box that takes a context through `opacity` or `transform` sorts where `auto`
  sorts.** CSS Color says exactly that: paint it where a positioned box at `z-index: 0` would go.
  Which is why `StackingOrder` gates on `IsPositioned` rather than reading `ZIndex` directly — an
  integer on a static box is ignored by CSS itself, so it is silent rather than reported.

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

## Traps in repeating a table header

`page/table_header` measures these against Chromium over three sheets, exact on all 99 boxes;
`RunningContentTests` keeps the cases a browser reference cannot express, each of which is about
something NOT being repeated.

- **The band starts at the TABLE's top edge, not the header's.** Measured: Chromium lands the first
  continued row 61.5px down a page whose header group is 61px tall, and the half pixel is the top
  rule the table draws above it. Reserving the header's own height instead puts every horizontal
  rule on the continuation page one device pixel high — 0.9185 against 0.9934 — which looks like an
  antialiasing residual and is an off-by-one. A CAPTION is excluded from the band, or a captioned
  table would carry the caption's height as blank space at the top of every page it continues onto.
- **It is a PAINTING change, and the corpus can only see it in pixels.** The box harvest runs
  against one continuous layout, so the browser reports the `thead` exactly once and an engine
  drawing it on page one alone matches the geometry comparison perfectly. Nothing in the tree moves:
  `Paginator` shortens the slice by the band and `PdfPainter` pushes the document below it, so the
  header box is the original drawn a second time through a translate.
- **A collapsed table's grid lines are not in the header's subtree.** They belong to the table, so
  re-drawing the group alone leaves it as unruled cells — `PaintHeaderLines` copies the lines lying
  WITHIN the band, and only those. A vertical rule spans the table's whole height, so it is left to
  the table's own paint; copied with the header it would run from the top of the page down past
  wherever the table ends on it.
- **Those lines were painted UNDER every cell background**, which `page/table_header` found on its
  first render. Under the collapsing model a cell's border box includes half of every rule around
  it, so a cell background reaches the middle of the line — and a header row with a fill of its own
  erased the rule beneath it entirely. CSS 2.1 Appendix E puts the collapsed borders after the
  backgrounds of all the table's elements; the fix moved them out of `Decorate` and to the end of
  the `Backgrounds` walk, and left every other scenario identical. Invisible until now because
  `table/collapse` measures six arrangements of rules and fills no cell.
- **The header is drawn between the in-flow layer and the positioned content**, which is where a
  copy of table content belongs. Before the page instead puts it under the root element's own
  background — which every stylesheet that colours the page paints over the whole sheet, so the
  header vanished — and after it puts it over a fixed running header it should sit below.
- **A grid line now has to be culled against the page.** It used to be drawn wherever it fell and
  clipped away by the page box, which was the same thing until a continuation page could push its
  content down: a line from the page BEFORE then lands inside the band, which is where it does not
  belong.
- **Reserving is bounded at half the page.** A header taller than that turns a continuation page
  into mostly header, and one taller than the page leaves the slice nothing to advance through, so
  the page count would follow the length of the document rather than its height.
- **`tfoot` is not repeated.** The footer is the header reflected — a band reserved at the BOTTOM
  and a second offset through the painter — and it is not reported either, because a `tfoot` on a
  table that fits on one page is perfectly correct and a report keyed on the element would fire on
  documents with nothing wrong with them.

## Traps in box-sizing

`block/box_sizing` and `image/percent_width` measure these, both exact against Chrome.

- **It applies to all six of `width`, `height` and the two min/max pairs**, not to `width` and `height` alone. So `max-width: 400px` under `border-box` caps the BORDER box, and the clamp has to happen before the auto margins are resolved — otherwise `max-width` with `margin: 0 auto`, the standard centring idiom, centres a box that is padding-and-border wider than it asked to be.
- **A declared size narrower than the box's own padding and border floors the content at zero**, leaving the box exactly as wide as its surround. Not negative, and not the declared value either: `width: 30px` with 50px of surround is a 50px box, which is what Chrome gives.
- **Eight sites turn a declared width or height into a used one, and only two of them are in `BlockLayout`.** A float's shrink-to-fit, an inline-block's assigned width, a table's own width, a table column's fixed width, the intrinsic pass and replaced sizing each resolve one themselves. `ComputedStyle.ContentSize` exists so that the property is applied in one place and a new site is a one-line call rather than a rediscovery; adding a site that skips it fails nothing, which is the usual problem.
- **The corpus's `flatten.css` has to restate `table { box-sizing: content-box }`** even though its `*` rule already says so. This is the AngleSharp origin trap in its purest form: in a browser the author `*` rule beats the user-agent `table` rule outright, and here the UA rule wins on specificity — so without the restatement a table with both a width and a border would be laid out border-box on one side and content-box on the other, and the difference would read as a table-layout defect.

## Traps in outlines, captions, object-fit and inside markers

`block/outline`, `table/caption_side`, `image/object_fit` and `block/list_position` measure these.
All four are geometry-exact; the middle two are pixel-identical.

- **An outline takes no space**, which is the whole property — a border in its place would shift the page every time focus moved. The scenario's last row exists to catch an outline that reserves space, since that moves every box below it.
- **`outline-offset` moves the ring's INNER edge, not its centre.** A 3px outline at offset 4 on a box at y=38 paints rows 31 to 33, so the gap is the offset and all the ink is outside it. The centre reading is off by half the width and invisible until measured.
- **An unsupported outline style draws NOTHING, where an unsupported border style draws solid.** An outline is decoration with no layout consequence, so the wrong ring is worse than none; a border has already reserved its space and something must fill it.
- **A bottom caption sits after the grid's TRAILING edge spacing, which the grid already added.** Giving it a gap of its own doubles it. Measured: Chrome puts a bottom caption exactly as far under the last row as a top one sits above the first.
- **`object-fit` never changes the box**, only what is drawn inside it — so the geometry comparison confirms it for free, and the four values need an image whose aspect ratio differs from the box's or they are indistinguishable. Measured with 64x32 in 160x60: `fill` 160x60, `contain` 120x60 centred, `cover` 160x80 clipped, `none` 64x32 centred.
- **The `inside` marker advance cannot be read off the ink.** The first glyph's left side bearing grows with the font size, which makes a clean rule look like a drifting one — the measurement has to come from a span's own rectangle. This cost a round.
- **An `inside` symbol marker's advance is `side + font-size + 1`**, in whole pixels off the whole-pixel ascent, and no fraction of the em fits: the ratio drifts from 1.375 to 1.325 between 16px and 40px because the symbol's own side moves in `SymbolSize`'s uneven steps. Measured exact at six sizes from 12px to 40px. A counter's advance is just the shaped width of `N. `, the same string the outside marker uses.
- **An `inside` marker and `text-indent` ADD.** Both narrow the first line from its start edge, and an indented inside list item starts its text past both.

## Traps in the collapsing border model

`table/collapse` measures these, and is pixel-identical to Chrome.

- **One line per boundary, centred on it, and every box's used border is HALF the line.** The table's own box therefore reaches half a line beyond its outermost cells. 2px uniform on 80px cells with 6px padding gives 94px cells inside a 190px table; 3px gives cells at x=1.5.
- **The widest border wins a shared edge**, then style, then origin — cell over row over row group over table. A 6px cell against 2px neighbours comes out 98px wide against their 96px; a 4px table border against 2px cells puts the cells 2px inside the table box.
- **The halving has to run BEFORE anything measures a cell.** The column algorithm sizes columns from cell border-box widths, so resolving afterwards sizes the table with one set of borders and paints it with another. Rewriting the boxes' styles is what lets the rest of table layout stay untouched.
- **The lines are painted once, never as two halves.** Two cells each drawing their own half seam at any odd width — 3px gives two 1.5px halves meeting on a half pixel, which antialiases into a visible join down every line. `#odd` is in the scenario for exactly this.
- **The WIDER line owns a crossing.** Lines run half a crossing line past each end so no corner is unpainted, which makes junctions overlap, and the list is sorted by width so the wider one goes down last. Painting the horizontals last regardless — the obvious way to fill corners — puts the wrong colour on all four corners of a framed table.
- **Rewriting a box's `ComputedStyle` breaks the reference identity line layout depends on.** A text node takes its parent's style INSTANCE, and that shared reference is what tells a block's own text from an inline box of its own — which is what keeps a cell's inherited `vertical-align: middle` from shifting the cell's text. Every collapsed cell came out 0.77px too tall, half an x-height, until `CollapsedBorders.Rewrite` repointed the inline items alongside the style. Anything else that replaces a style has to do the same.
- **`border-style: hidden` cannot be honoured.** CSS gives it absolute priority at a shared edge, beating even a wider border. `StyleResolver` folds `hidden` into a zero width before anything downstream sees it, so it is indistinguishable from an absent border and loses on width. Reported, and only inside a collapsed table, since everywhere else the two really are the same.

## Traps in transforms

`block/transform` measures these, and is both pixel-identical and geometry-exact against Chrome.

- **`getBoundingClientRect()` returns the VISUAL rectangle**, so a rotated 60x40 tile comes back as the 71.96x64.6 box enclosing it. The corpus compares against that rect, so a transformed element is either exempted from the geometry comparison or reported transformed on this side. The second was taken: `BoxDump` computes the visual box, which turns the comparison into a real check of the matrix arithmetic instead of a hole in it.
- **`BoxDump.Collect` walks recursively for that reason.** A transform applies to a box AND its descendants, so a transformed box inside another carries both, and a flat walk over `Descendants()` has nowhere to keep the matrix that says so. The painter gets the same composition free by nesting its pushes.
- **Functions compose left to right, which means the RIGHTMOST reaches a point first.** `translate(30px) rotate(15deg)` rotates the box about its origin and then moves it, rather than moving it and rotating about where it started. The two differ by where the pivot lands and both look reasonable, which is why `block/transform` has a row for it.
- **The origin is the centre of the border box**, so the whole matrix is conjugated by it — moved there, applied, moved back. Every function turns about the coordinate system's zero, and CSS turns them about the box.
- **A transform changes painting and not layout.** The box keeps the space it was given and its siblings sit where they would have, the same bargain `position: relative` strikes.
- **It creates a stacking context**, so it reuses the machinery `opacity` built. The transform is pushed OUTSIDE the fade, so a box carrying both is faded and then drawn through the transform.
- **The three-dimensional functions are left unparsed rather than flattened.** `rotate3d` has a two-dimensional shadow that would put the box somewhere plausible and wrong, so the whole transform is dropped and reported.
- **The cascade does NOT normalise `transform`** — values arrive verbatim, unlike a gradient's corner keyword. It does reorder `transform-origin` so the horizontal component comes first, which is what lets the two be read positionally.

## Traps in gradients

`block/gradients` measures these. Geometry is exact and no pixel differs by more than two of 255.

- **The default direction is `to bottom`, not `to right`**, and interpolation is plain linear sRGB — the midpoint of two stops comes back as the exact arithmetic mean of their channels.
- **The gradient line's length is `|W·sin A| + |H·cos A|`**, which is what makes a 45° gradient put its start colour exactly on one corner and its end exactly on the opposite one whatever the box's proportions. It is the one piece of the geometry that is not obvious, and one sampled pixel confirms it: 177 measured against 177.2 predicted.
- **A radial gradient defaults to an ELLIPSE sized `farthest-corner`**, whose radii work out at exactly √2 times the half-width and half-height. So the left edge of a 200×60 box sits at 0.7071 along the ramp rather than at its end.
- **A browser TILES the gradient, and its box is the PADDING box.** `background-repeat` defaults to `repeat` and the paint reaches the BORDER box, so the strip under a border carries the end of the previous tile — bluish at the left border of a red-to-blue ramp, which is the opposite of what padding the edge colour gives. An axis-aligned ramp is uniform perpendicular to its own axis, so repeating along that axis IS the two-dimensional tiling; that is used only where the paint actually reaches past the gradient's box, since repeating inside it wraps the last column back to the start colour.
- **A hard stop cannot be expressed directly.** A PDF shading's stitching function needs strictly increasing bounds, so two stops at one offset leave a zero-width step that is dropped and the edge becomes a ramp across the whole box. Nudging the second onto the next representable float keeps it within a sub-pixel.
- **The background colour and image are two LAYERS**, not alternatives: the colour goes down first and a translucent ramp shows it through.
- **A gradient's `AE` is high and means nothing.** A smooth ramp has thousands of pixels a shade apart from the browser's quantisation of the same ramp, and `AE` counts any nonzero difference. SSIM and the maximum per-pixel delta are the numbers to read here.

## Traps in opacity

`block/opacity` measures these, and is pixel-identical to Chrome.

- **`opacity` is not a fill alpha, and the difference is measurable.** The box and everything under it are drawn into a group and the GROUP is faded, so two overlapping children of a half-opaque parent show the same shade in the overlap as anywhere else. Fading each fill on its own darkens it. That is the whole reason the property needs a stacking context.
- **The group has to be ISOLATED as well as faded.** Without the isolation krilla applies the alpha to each drawing operation as it goes down rather than to the finished group, which produces exactly the darkened overlap above.
- **A box with opacity below 1 leaves its parent's phases and paints with the positioned content.** So a faded box written FIRST covers an opaque sibling written after it. Document order gives the opposite answer and looks entirely reasonable until it is rendered — `block/opacity`'s last row is the one that tells them apart.
- **`PaintContext` recurses only for a box that establishes a context.** A positioned box's own positioned descendants are already flattened to the page by the walk that found it, so recursing there paints every one of them twice. That is how this first ran, and the four `position/` and `page/absolute_break` scenarios reported it at once.
- **`Hoisted` must not descend INTO a stacking context.** Whatever is positioned inside a faded box belongs to that box's group; flattening it to the page paints it at full strength beside its faded siblings.
- **A float or an inline-block carrying opacity is faded where it is painted**, not hoisted. CSS puts it in the same step as the boxes above, so the two differ only when something overlaps such a float — which no scenario measures.

## Traps in decorations, sizing keywords and borders

`text/font_size_keywords`, `text/decorations`, `inline/vertical_align`, `block/border_styles` and
`block/border_radius` measure these. All five are exact on geometry; `text/decorations` and
`block/border_radius` are pixel-identical.

- **The `font-size` keyword table is not a series.** 9, 10, 13, 16, 18, 24 and 32 pixels: one-pixel steps at the small end and eight-pixel steps at the large end, so no single ratio reproduces it. It is anchored on a CONSTANT rather than on the root element, which is what CSS asks for and what a browser does — the table follows the reader's preferred size, so `font-size: large` stays 18px however the document sizes `html`. The two relative keywords are the parent's size over or times 1.2, and resolve against the INHERITED size: `smaller` inside a `large` parent is 15px, not 13.333.
- **`initial` on `font-size` is `medium`**, and now resolves rather than reporting. It arrives far more often than anyone writes it, since a shorthand that omits a component sets that component to it.
- **A decoration's position comes from the font, and is ROUNDED.** Underline from `post`, line-through from `OS/2.yStrikeoutPosition`, and overline from the rounded ascent, since no table carries one. At 16px Liberation Sans that is baseline + 1, baseline − 5 and baseline − 15, and all three agree with Chrome to the pixel row. Unrounded, the strike straddles two rows at partial coverage and reads as a grey smear.
- **`vertical-align`'s super and sub offsets are not font metrics.** Measured across three sizes they are linear in the font SIZE with an intercept of exactly one pixel — `size / 3 + 1` up and `size / 5 + 1` down. This face's OS/2 superscript offset is 7.63px at 16px where the browser uses 6.33, so reading the font would have been confidently wrong.
- **Every `vertical-align` keyword measures against the PARENT's font**, not the aligned box's own. Giving the box its own `font-size: 10px` inside a 32px paragraph moves it not at all — which matters, because the default stylesheet makes every `<sup>` smaller than its parent.
- **`middle` reads the x-height unrounded** where `text-top` and `text-bottom` read the ascent and descent rounded. The ratio holds at 0.5283 of the size at 16, 24 and 32 pixels, which is this face's `sxHeight` over its em.
- **`vertical-align` is inherited here and CSS does not inherit it**, which is deliberate — the user-agent sheet gives a table `middle` and its cells `inherit`, so a cell can only read it by being handed it. The cost is that every run of text in a cell carries `middle` too, so the inline half applies only where the value was DECLARED and only to a token that is not the block's own text. Both guards are needed; without them every table scenario moves at once.
- **Chrome truncates lengths onto a 1/64 pixel grid**, so `16/3 + 1` is stored as 6.328125 rather than 6.3333. A fortieth of a pixel, and still visible: the line it sizes ends on a fractional row and the background painted to that row comes out a different shade. Quantising took `inline/vertical_align` from 0.9969 to 0.9989.
- **A decoration's COLOUR inherits with the decoration and not with the text.** An element that starts a decoration of its own starts its own colour with it; one merely inheriting an ancestor's rule keeps the ancestor's colour. Reading `text-decoration-color` off the run's own style gives the second case the wrong colour whenever a descendant sets `color`.
- **A patterned decoration is drawn at TWICE the thickness a solid one gets, and CENTRED on it.** Chrome's dashes are six pixels on and four off under a rule that is one pixel thick when solid — both multiples of two, which is what says the width is two. Blink's own patterns are three widths on and two off for a dash and one each way for a dot, and both land exactly on the measured numbers at that width. Hanging the thicker rule below the solid position instead of centring it puts it a row low, which was the whole of the difference between 0.9989 and 0.9999.
- **A double rule is two lines separated by TWICE their thickness**, so a 1px underline at baseline+1 puts its second line at baseline+4. Two lines 2px apart is what a reading of the specification suggests and is not what Chrome draws.
- **No border-style geometry is specified.** Measured: a dash is twice the border's width with a gap of its width, a dot is the width across and repeats at twice it, and a double border is two bands a third of the width each. Which is why `border: 1px double` is indistinguishable from solid.
- **A dot is ROUND**, so it is drawn as a zero-length dash under a round cap rather than a square one. Indistinguishable at 1px and obviously different at 8.
- **A patterned edge does not mitre.** Dashes run past the corner and a double border's two bands span the whole side, so those edges are stroked along their own centre lines while solid edges still take the trapezium path — which `block/border_styles`' mixed row keeps honest.
- **A rounded corner is a bezier at kappa, because that is what the browser draws.** Matching the browser's CONSTRUCTION is what makes `block/border_radius` pixel-identical; a more accurate arc would agree less. The same lesson `ListMarkers` records about stroking a circle rather than filling an annulus.
- **An over-large radius scales the WHOLE box, not each side.** `border-radius: 999px` on a box 40px tall scales every radius by one factor, the smallest that makes each side fit. Clamping per side gives a rectangle with mismatched circular ends where a browser gives a pill.
- **An inner radius is the outer one less the edge it runs along, floored at zero**, which is what makes a thick rounded border read as a ring rather than a tube.

## Traps in overflow, visibility and text spacing

Five properties that were read and not honoured, each measured out of Chrome before being written.
`block/visibility`, `block/overflow_hidden`, `float/overflow_bfc`, `text/text_transform`,
`text/letter_spacing` and `text/word_spacing` measure them, and all six are exact on geometry.

- **`visibility: hidden` is not `display: none`, and the box still occupies its line.** Reading it as removing the box closes the gap it left and moves everything below up by its height — which the geometry comparison catches at once, and which is why the scenario leads with the plainest possible arrangement.
- **The visibility check has to be per RUN, not per box.** The property inherits and hiding is only not-painting, so a descendant setting `visible` inside a hidden parent is drawn — one line can hold hidden and visible text at once, and a box-level test paints all of it or none.
- **An `overflow` clip is pushed inside each PHASE, not once around the box.** Painting the subtree as one unit under a single clip is what a stacking context does and `overflow` does not create one: it would put the box's text down during the background phase, where a later sibling's background could cover it — the defect `block/overflow_paint` exists to catch. Each phase visits a subtree as one contiguous stretch, so a clip held across that stretch preserves the global phase order.
- **The clip is to the PADDING box**, so a box with padding shows content inside the padding and cuts at the inner edge of its border.
- **An `overflow` box is placed BESIDE an outside float, not overlapping it.** This is the exception to the most surprising float rule — an ordinary block keeps its full width and lets the float overlap it. It is also the reason the property gets written at all: a float with a text block beside it is the pre-flexbox media object, and `overflow: hidden` on the text block is how that was done. Chrome narrows AND shifts the border box: 120px float, container at x=120 with width 696.
- **The float band must be probed as an infinitesimally thin slice, not a zero-height one.** `FloatContext.Band` treats its range as half-open, so a zero-height query overlaps nothing and every box comes back full width. That is exactly how `float/overflow_bfc` failed on its first run.
- **The band is sampled at the box's TOP EDGE and holds for its whole height.** The container is 60px tall beside a 90px float and stays narrow; nothing re-widens below the float's bottom.
- **`text-transform: capitalize` does not capitalise after an apostrophe or a digit.** `page-break o'clock 3rd (bracketed) "quoted"` becomes `Page-Break O'clock 3rd (Bracketed) "Quoted"`. The obvious rule — a word starts after any non-letter — gives `O'Clock` and `3Rd` and is wrong in two cases out of five. The rule that reproduces Chrome is per preceding character: a letter starts a word unless what precedes it is a letter, a digit, or an apostrophe.
- **Casing is applied after white-space processing and before shaping.** It changes the advances, so it cannot be a painting concern — upper-casing makes a line half again as wide and wraps a paragraph a line earlier.
- **`letter-spacing` counts after the LAST character too.** Seven characters at 3px are 21px wider, not 18px. Only a shrink-wrapped box can show this, which is why every row in `text/letter_spacing` is an inline-block.
- **`letter-spacing` is per CHARACTER, not per glyph, and the ligature survives.** `office` with 3px is 18px wider — six characters — while the `ffi` stays one glyph and the base width is unchanged. Per glyph is the reading a shaper invites and gives 12px. The extra advance is attributed to the glyph covering the characters it is owed to, which keeps the painted width equal to the measured one however the shaper grouped the text.
- **`word-spacing` is per SPACE, not per word.** Three words and two spaces at 10px gain 20px, not 30px. `text/word_spacing`'s one-word row is what makes the two readings distinguishable, since they otherwise differ by one increment everywhere and both look plausible.
- **Both spacings belong inside `ShapedText`.** They change the answer to every question it exists to answer, and adding them afterwards at some call sites and not others is the shape that bug takes.

## Traps in line breaking

`inline/hyphen_breaks` and `inline/word_joins` measure these, both exact against Chrome on
geometry and pixels; `table/hyphen_columns` measures what they do to intrinsic widths. Every rule
was measured one arrangement at a time rather than read off UAX #14, and that was worth doing —
the exceptions a careful reading of the specification suggests turn out not to exist.

- **A break opportunity is a property of a TOKEN, not of the gap between two of them.** Line breaking used to treat adjacency as an opportunity, which was invisible while a space was the only thing that ever produced two tokens. It stops being invisible the moment anything else does: `<span>abc</span><span>def</span>` is one word to a browser and this engine split it at the element boundary. So a hyphen fix that merely emitted more tokens would have inherited the bug and rendered the hyphen case correctly by accident. `Token.BreaksBefore` carries it instead.
- **There is no numeric exception, and no leading-dash exception.** `1234567890-1234567890` wraps at the hyphen, and `-abcdefghij` puts the dash alone on the line above. Both are what UAX #14's numeric-sequence rules and a reading of "don't break a word open at its start" would suggest suppressing, and Chrome suppresses neither. Every hyphen-minus with something after it offers a break.
- **A run of dashes needs no rule.** Every dash offers an opportunity and greedy line breaking takes the last that fits, so `a--b` keeps both dashes on the line above by arithmetic. Writing a rule for the run produces the same answer and hides that.
- **A dash at the end of an inline element still breaks the next one.** `<span>page-</span><span>break</span>` breaks where `<span>page</span><span>break</span>` does not, so breakability crosses the element boundary even though the boundary itself is not an opportunity. That is what makes `breakable` a variable carried across items in `Tokenize` rather than reset per item.
- **A break IS allowed before an atomic inline with no space.** Measured, and it is the rule that keeps the element-boundary fix from over-correcting: "adjacency is never an opportunity" would glue an image to the word before it.
- **The en and em dashes break; U+2011 and the solidus do not.** The non-breaking hyphen is the whole point of that character. The solidus is worth knowing because a URL is the obvious thing a reader expects to wrap, and Chrome does not wrap one.
- **Splitting happens at TOKENISATION, over one shaped run.** Each segment's width is a sub-range of the same `ShapedText`, so the segments sum to exactly what the whole word measured and the kerning across the dash survives. Shaping the segments separately would lose both.
- **A break opportunity changes MIN-CONTENT width, and nothing fails when that is missed.** A hyphenated word's minimum is its longest segment, so a table column sized from the whole word comes out too wide and a cell that should have wrapped does not. `table/hyphen_columns` is 23px wide and one line short without the reset in `Intrinsic` — and no other scenario in the corpus moves at all, which is exactly why it needed a scenario of its own.
- **Soft hyphen is unimplemented rather than decided against.** It is a break opportunity AND paints a hyphen only when the break falls there, which is a conditional glyph rather than a break rule.

## Traps in page breaks

`page/break_before`, `page/break_after` and `page/break_inside` measure these, all three exact
against Chrome on geometry and the first two pixel-identical.

- **A forced break ADDS a page; it does not choose where an existing one ends.** Every other rule in `Paginator` moves a boundary the document height had already made necessary, so the loop ran while `top + pageHeight < documentHeight` — which is exactly the condition a forced break in a short document fails. Three boxes totalling 144px on a 1056px page still want two pages if one of them said so, and the property can be read, resolved and looked up correctly and still produce one page with nothing in the box geometry to say so.
- **`break-after` resolves to the top of the NEXT in-flow box, not to the declaring box's bottom edge.** Measured: with a 40px margin between them, Chrome starts the next page at the following box's top and the margin is gone entirely — not drawn at the foot of the page before, not drawn at the head of the page after. Breaking at the bottom edge instead puts every box on that page 40px low under a band of margin. `page/break_after` carries a margin for exactly this reason, and a version without one passes against either implementation.
- **The next in-flow box is not the next SIBLING.** A `break-after` on a box that is its parent's last child belongs to whatever follows the parent, which is why `ForcedBreaks` walks pre-order and keeps a subtree end per box rather than looking sideways.
- **A break at the start of the document, and a break after the end of it, are both dropped.** `page-break-before: always` on a section wrapper matches the first section as readily as the rest, so honouring it literally opens every such document with a blank page. A browser does emit a trailing blank page for a `break-after` on the last box; this does not, on the grounds that a blank final page is the less useful of the two answers.
- **`break-inside: avoid` is a table row reached from the other direction.** A row is an unbreakable unit by rule and an `avoid` box is one by request, and they are the same thing to the slice — a rectangle that moves whole. Which means the too-tall case was already answered: `NextTop`'s height guard lets it overflow rather than descending forever looking for room, the same guard a too-tall row uses.
- **`avoid` at a box EDGE is a different request from `avoid` INSIDE a box, and only the second is implementable here.** `break-inside` names a rectangle to keep together; `break-before: avoid` asks for a break to be moved somewhere earlier, and the slice has no notion of rejecting a candidate in favour of one further back. It stays in the diagnostic table.
- **Breaks on out-of-flow boxes are ignored, structurally.** `ForcedBreaks` walks `Children` alone, so a float or an absolute box contributes nothing — which is also CSS's rule, since neither is at a flow position a page could start at. It is worth knowing that this is a property of the walk rather than an explicit test, because a walk extended to `Floats` or `Positioned` for some other reason would silently change it.
- **Both spellings have to be read.** The cascade does not alias them: a `page-break-after` declaration comes back under that name and nothing comes back under `break-after`. Reading one and not the other halves the documents the feature works on, while every test written in the spelling that was read still passes. The legacy spelling is the one that matters more in practice, being what reporting tools and mail merges emit.

## Traps in the inline box model

`inline/backgrounds` and `inline/padded` measure these, both exact on geometry and both at SSIM
1.0000. Nothing here was reachable before text runs started carrying the selector of the element
they came from, which is the point the section below about measurement makes.

- **An inline element generates no box, so the corpus could not see one at all.** Eighteen elements
  across six scenarios were counted `unmatched` and looked measured while being measured by nothing
  but pixels. `TextRun.Selector` plus a union of its fragments in `BoxDump` fixed that, and the fix
  immediately found the engine painting no background on an inline element — which is what a new
  measurement is for.
- **The rectangle around inline text is the FONT box, not the line box.** Baseline less the
  whole-pixel ascent, as tall as that ascent plus the whole-pixel descent: 17px for 16px Liberation
  Sans and 14px for the same face at 12px, which is not a fixed ratio of the size. A generous
  `line-height` moves the fragments apart without making any of them taller, so filling the line box
  is indistinguishable at the common line heights and wrong at every other.
- **A background is one rectangle per LINE FRAGMENT.** One rectangle round the whole element colours
  the blank space at the end of each line and the indent at the start of the next.
- **An enclosing inline element is reported and painted at ITS OWN height.** A run carries the style
  of the INNERMOST element it sits in, so without recording the ancestry there is nothing to paint an
  outer highlight from and it comes out with a hole where the nested word sits. And the outer's box
  is its own font box rather than the union with the inner's: a plain `<span>` holding a bordered one
  is two pixels shorter at each end than the union would be. Measured.
- **Ancestors are reachable without extra bookkeeping, because a selector path is its own ancestry.**
  Every prefix of `body > p > b > i` names an enclosing element. Prefixes that turn out to be
  block-level are filtered by having produced a box of their own.
- **Horizontal padding on an inline element is LAYOUT, not painting.** It advances the text after it,
  so it decides where every later word on the line goes. Vertical padding is painting alone: it
  overflows the line box rather than growing it, so two lines of a padded inline overlap each other's
  padding rather than being pushed apart.
- **The left border goes on the fragment holding the element's opening edge and the right on its
  closing one, and neither anywhere else.** A padded inline that wraps is open at the break, because
  the element did not end there.
- **The horizontal margins apply and the vertical pair does not**, which is CSS's own rule. The
  margin is advance outside the border box, so it is carried in the edge token's width and taken off
  again when the box is placed.
- **An inline fill has to be SNAPPED to whole pixels.** A fragment from 49.81 to 127.21 is painted
  over columns 50 to 126 with hard edges at both ends. Left fractional it picked up one whole extra
  column of colour at every fragment's right edge, because the rasteriser reading the PDF snaps
  outward where the browser snaps to nearest. That column was the whole of the difference between
  0.9999 and 1.0000.
- **A fit test has to measure the whole UNBREAKABLE RUN, not the token at the opportunity.** Padding
  exposed this: the leading edge fitted, the word after it could not break, and the line overran its
  band. The two readings agree wherever a break opportunity is followed by something that offers one
  of its own, which is nearly always.

## Traps in the value layer

`block/calc`, `block/viewport_units` and `inline/vertical_align_length` measure these, all three
exact on geometry.

- **AngleSharp hands `calc()` back verbatim**, exactly as it does a bare percentage, so evaluating it
  falls to this engine. Before that it fell through to the "unparseable" fallback and the property
  took its default — which for a `width` means the box fills its container, a whole-page difference
  no diagnostic reported. **A value nothing recognises is a value nothing can report**, which is the
  general lesson: the diagnostic table cannot cover a syntax the resolver does not parse.
- **Only a MIXED `calc()` needs a kind of its own.** All-absolute folds to a length and purely
  proportional folds to a percentage, so the twenty-odd sites deciding whether a length is definite
  by testing for an absolute one keep answering correctly without knowing the syntax exists. A mixed
  one is not definite — it depends on the containing block exactly as a percentage does.
- **Whitespace around `+` and `-` inside `calc()` is REQUIRED**, which is CSS's rule rather than a
  shortcut. Without it `calc(2e-5px)` and `calc(10px -5px)` cannot be told apart.
- **A viewport unit is not a percentage.** It does not depend on the containing block, so it resolves
  during parsing and never reaches layout as anything but an absolute length. In paged media the
  viewport is the page's CONTENT box, which is what a browser printing to PDF resolves against.
- **`vertical-align`'s percentage resolves against the element's own `line-height`**, not its font
  size. At a 24px line, `25%` lands a box exactly where `6px` does. And unlike every keyword — which
  measures against the PARENT's font — a length is ordinary value resolution against the element that
  declared it, so `0.5em` on a 12px span inside a 16px paragraph is 6px rather than 8.
- **AngleSharp DROPS three things rather than passing them through**, and each is a case that cannot
  be honoured or reported because it never arrives: `revert`, `text-overflow`, and the
  `min-content`/`max-content`/`fit-content` sizing keywords. `unset` does arrive, and is a no-op for
  everything in the diagnostic table for the same reason `initial` is.

## Traps in soft hyphens, word breaking and tabs

`text/soft_hyphen`, `text/word_break` and `text/tabs` measure these. All three are exact on geometry
and pixel-identical to Chrome.

- **A soft hyphen must be stripped BEFORE shaping.** This face maps U+00AD onto a real hyphen glyph
  with a real advance, so a word carrying two of them measured wider than the same word without and
  drew hyphens no break called for. Stripped, an unbroken word measures exactly what it would with
  nothing in it — which is the whole property, and what `text/soft_hyphen`'s `#wide` row asserts by
  matching `#none` to the hundredth of a pixel.
- **The drawn hyphen belongs to the ELEMENT that broke.** Chrome reports the span 5.33px wider than
  the text inside it, so the hyphen run carries the selector and the inline ancestry and a background
  reaches under it. The opposite was assumed first and measurement said otherwise.
- **The hyphen's width is not in the fit test**, only in the line it ends. A line leaving less than a
  hyphen of slack overruns by up to that much where a browser would move the segment down; correcting
  it needs the line breaker to back up over a decision already taken. Known, and recorded in the
  scenario's notes.
- **`break-word` and `break-all` differ, and a single long word cannot tell them apart.** The row
  that does — ordinary words before a long one — found a defect: the long word was MOVED to a fresh
  line by the ordinary break rule and added there directly, so nothing afterwards asked whether it
  could be cut. A word moved to a fresh line has to fall through to the splitting loop.
- **Splitting walks outward rather than binary-searching**, because `ShapedText` answers a sub-range
  by summing advances it already holds: the walk costs one width query and yields every candidate on
  the way. A cut takes at least one character on an empty line whatever the width, or a character
  wider than the box loops forever.
- **No hyphen is drawn at a forced cut.** The break came from the box rather than from the text.
- **A tab's width is not known when it is measured.** Where the stop falls depends on how far along
  the line the tab already sits, so the token carries the STOP SPACING and the advance is settled
  while the line is being filled — the one place a token's width field means something other than
  how wide it is.
- **Tab stops are at multiples of `tab-size` SPACE ADVANCES from the line's start edge**, and a tab
  already exactly on one advances to the NEXT rather than to nothing. Nine characters followed by a
  tab reach 16 rather than 12.
- **A tab has to be kept out of run merging.** Its text range is contiguous with the words either
  side, so it joined their run and the tab CHARACTER was drawn as a glyph — at the glyph's own
  advance rather than the distance to the stop, which moved every glyph after it inside the run. It
  is excluded from the unbreakable-run widths for the mirror-image reason.

## Traps in background images

`block/background_image` measures these across sixteen rows and is pixel-identical to Chrome.

- **A stylesheet `url()` goes through the same `ImageStore` an `<img src>` goes through.** That is the
  point rather than a convenience: a stylesheet is the part of a document a reader is least likely to
  have read, so it would be a poor place to leave the image policy unenforced.
- **Two rectangles, and they are not the same one.** The image is POSITIONED against the padding box
  and PAINTED out to the border box, so the strip under a border carries the tail of the previous
  tile rather than the head of the first. Which means a repeat has to be BACKED UP to the last tile
  reaching into the painted area rather than started at the positioned one.
- **A percentage position does not offset by a fraction of the box.** It aligns that fraction of the
  IMAGE with the same fraction of the box, so `25%` on a 64px image in a 200px box lands at 34 rather
  than 50. `center` is exactly `50%` under the same rule and `right` exactly `100%`, which is what
  puts an image's far edge on the box's far edge rather than off it.
- **The origin is snapped to whole pixels.** `75%` of the 38px left over is 28.5 and Chrome starts the
  tile on row 29 rather than straddling two rows at half coverage. Same construction argument as the
  inline background fill, and the last pixel between 0.9991 and 1.0000.
- **`background-clip` and `background-origin` move those two rectangles independently**, so
  `content-box` clipping makes the image look CUT along the content edge rather than moved — its
  position still came from the padding box.
- **The tile count is bounded at 512 per axis.** A `background-size` resolving to a fraction of a
  pixel would otherwise ask for hundreds of thousands of draws.
- **`object-position` is the same rule as `background-position`**, and it has to apply AFTER
  `object-fit`: under `cover` the slack goes negative and the offset chooses which band of the image
  survives the clip.

## Traps in list marker images

`block/list_image` measures these, exact on geometry.

- **`list-style-image` is a LAYOUT change, not a painting one.** An item whose marker is a 32px image
  is 39px tall, not 24: the marker is an atomic inline on the item's first line, bottom edge on the
  baseline, so it grows the line exactly as an inline image of that height does. That is why the
  image is prepended to the item's own inline content rather than handed to `ListMarkers` — a marker
  drawn beside the item could not have grown it.
- **`outside` takes no advance and `inside` takes the image's width PLUS the marker gap.** Both use
  the same seven pixels a symbol marker leaves, which is the same constant because it is the same
  gap. Reading the inside advance as the width alone is seven pixels out, and the pixels invite that
  mistake: the item's background becomes visible exactly at the image's right edge, so the image
  looks like the whole advance until the TEXT is measured instead of the fill.
- **An image that does not resolve falls back to the counter style**, which is what Chrome does — so
  the check is on the RESOLVED image rather than on the declaration, and the counter marker has to
  stay in the box tree to be fallen back to.
- **An item whose whole content is a block keeps its counter marker.** There is no line here to hang
  the image from. A browser puts the marker on the first line wherever it is, including inside a
  nested block, so this is a limitation rather than a rule.

## Traps in paged media

`PageRuleTests`, `RunConstraintTests` and `SidedBreakTests` cover these. The corpus cannot: its page
size is fixed so the reference and the render rasterise to the same dimensions, and a scenario
changing it would suppress SSIM rather than report a difference.

- **A PDF is PRINT, and media queries have to resolve that way.** Against `Screen`, a document's
  `@media print` block — the one written for this conversion — was excluded while its `@media screen`
  block was applied. The corpus reference already came from Chromium's printer for its page renders;
  its box harvest now emulates print media too, so the two halves of one reference agree about which
  rules apply. Regenerating all references changed nothing, which is what says the fix was free.
- **It was NOT free in the engine, because AngleSharp's print defaults are the HTML 4.01 sample
  sheet.** `h1 { page-break-before: always }`, headings avoiding a break after, lists avoiding one
  before — rules no browser implements. The corpus said so within a second: `ua/headings` grew a
  second page against a reference that has one, and a readme sample went from one page to two. All
  three are neutralised in `UserAgentStyles.Corrections`, which exists for exactly this.
- **`@page` decides the paper by DEFAULT.** A document declaring A4 means it, and printing it onto
  Letter is a whole-document difference with nothing to explain it. The geometry has to be settled
  BEFORE the cascade is read, or a document declaring its own paper is laid out against the wrong
  rectangle — a viewport unit is the cheapest way to see that.
- **AngleSharp keeps `@page`'s margins and DROPS its `size`.** The rule's own `CssText` comes back
  without it and `Style.GetPropertyValue("size")` is empty, so that one declaration is recovered from
  the stylesheet's own text. A scan of CSS source is not something to reach for twice, and it is
  bounded on purpose: only inside `@page` blocks, only a `size` declaration, stopping at the first
  closing brace, and reporting anything it cannot read.
- **One length in `size` is a SQUARE page**, which is the specification's rule and not what an author
  writing a width expects. Two lengths are not turned by an orientation keyword — they are already in
  the order the author wanted — while a keyword alone turns whatever paper the caller chose.
- **`orphans` and `widows` are implemented and OFF by default — and the reason recorded here for a
  long time was WRONG.** It said Chromium does not implement them. It does: a probe of a four-line
  paragraph whose natural break would leave one line above moves the whole paragraph to the next
  sheet, and raising `orphans` to three on `page/break_between_lines`' own arrangement moves it
  there too. What Chromium does differently is the case where NEITHER constraint can be met — a
  three-line paragraph under `orphans: 2; widows: 2`, which is exactly `page/break_between_lines`.
  There it splits two and one anyway; this moves the whole run overleaf. So the switch is not the
  choice between typography and fidelity it was documented as, and turning it on today costs that
  one scenario 0.9996 for 0.9769. Turning it on and relaxing the unsatisfiable case to split is
  the change that would agree with the browser, and it is not made yet. The move must ADVANCE the
  page top either way, or the loop that produces page tops never ends.
- **A sided break inserts a blank page, which is a page COUNT difference.** `right` lands its content
  on an odd page and `left` on an even one, counting page one as a right-hand sheet. The rule that
  drops a break at the very start of the document applies to them too, or `right` on the first element
  opens with a blank page and lands the content on an even one anyway.

## Traps in page margin boxes

`PageMarginBoxTests` covers these. The corpus cannot: **Chromium implements none of `@page`'s
margin boxes**, so a reference generated from a document declaring an `@top-center` comes back with
an empty margin — a scenario would record the browser's absence as the target and fail the moment
the feature worked. The same shape as `orphans` and `widows`, and resolved the other way, because
a running header is not a fidelity question: no browser produces one, so there is nothing to
disagree with.

- **AngleSharp drops the whole of this.** Not just `size`, which was already known: the SELECTOR
  comes back empty and a margin box at-rule has no object at all. So the `@page` scan is now a
  brace-matching one over the stylesheet's own source, yielding each block's selector and body, and
  everything is recovered from that — which is also why it is ONE scan rather than one per thing
  recovered. Depth counting rather than a search for the next `}` is what lets a margin box be
  found at all: a nested at-rule has braces of its own and the first close belongs to it.
- **The scan cannot see the rule tree above it**, so an `@page` inside an `@media` block is read
  whatever the query says. A PDF resolves media queries against PRINT, so the block that matters —
  `@media print` — is the one this gets right by accident.
- **A margin box is built PER PAGE**, which `counter(page)` forces: it has a different answer on
  every sheet, so its content, its layout and its width are all settled while that sheet is painted.
  `counter(page)` and `counter(pages)` are special-cased there and nowhere else; a DOCUMENT counter
  has no value on a page, the page not being a position in the tree, and is reported.
- **It is laid out in the PAGE's coordinates, not the document's**, and painted outside the clip
  everything else goes through. That clip is the content box, which is the whole point of a margin
  box — it sits where nothing in the document can reach.
- **Its declarations are carried by an element that is never in the document.** That is what keeps
  it out of the cascade, as CSS asks: its parent is the page context rather than the body, so
  `body { color: grey }` leaves the footer black. `element.GetStyle()` gives the inline declaration
  with shorthands expanded, which is exactly the margin box's own declarations and nothing else.
- **The FONT FAMILY is the one concession, and it is not inheritance.** The page context has no
  font, and the only other answer is `FontSet.Fallback` — which in a set of Liberation faces is the
  MONOSPACE one, so a running header came out in a typewriter font. The root element's family is
  used instead: a document has exactly one obvious answer to what its text looks like.
- **Two rules for one slot are CONCATENATED, not merged property by property.** A declaration block
  resolves later declarations over earlier ones, so joining the blocks in cascade order and parsing
  the result once IS the cascade — and is the cascade for shorthands too, which merging by property
  name would have to reimplement.
- **`:first` and `:blank` outrank `:left` and `:right`, which outrank no selector at all.** CSS
  Paged Media's own order, and what every "no header on the title page" stylesheet depends on: the
  bare `@page` rule is written first and must not win back the page `:first` named.
- **A NAMED page selects nothing.** `@page cover` matches the elements carrying `page: cover`, a
  property this engine does not read — so applying it to every page instead would put a cover
  sheet's header on all of them, which is worse than the header being absent and much harder to
  attribute. Dropped, and reported.
- **`content` decides whether the box exists**, which is CSS's rule and a useful one: `content: none`
  on a selector is how a stylesheet takes the header off a page, and it takes the box's border and
  background with it.
- **A selector cannot vary the GEOMETRY.** `@page :first { margin-top: 3in }` is read for its margin
  and applied to every page: a page whose content area differs from the rest is a different layout
  rather than a different painting, and the document is laid out once.
- **The three boxes in a strip are not divided between.** CSS Paged Media §5.3 computes each one's
  max-content and min-content widths and distributes the remainder; each is given the whole strip
  here and placed by its own alignment instead. The two agree wherever one box in a strip has
  content, which is nearly always, and differ only when two long ones share a strip — where this
  lets them overlap rather than wrapping them early. Not reported, because there is no browser to
  measure the difference against and nothing an author could act on.
- **`string()` cannot be reported.** It is CSS's own running-header mechanism, paired with
  `string-set`, and it is not implemented — but the reason it is silent is that AngleSharp DROPS the
  declaration carrying it, so `content` comes back empty and is indistinguishable from a margin box
  that declared none. The same shape as `revert` and `text-overflow`.

## Traps in generated content and counters

`block/generated` is pixel-identical and `block/counters` geometry-exact. AngleSharp hands the whole
`content` grammar back verbatim, which was the pleasant surprise — but two things about its
serialisation had to be worked around, and both were found by measuring.

- **The pseudo-element cascade INCLUDES the host's own declarations.** Ask a `::before` for its
  `display` and the host's comes back. So a property counts as the pseudo's only when it differs from
  what the host's own cascade says, which is sound because a `::before` selector does not match the
  element. Without that test, `p { content: "x" }` — a declaration CSS itself ignores — generated a
  pseudo-element on every paragraph in the document.
- **`counters(section, ".")` comes back as `counters(section .)`**, comma gone, while
  `counter(chapter, upper-roman)` keeps its. Splitting arguments on commas alone read the whole of the
  first as one argument and silently dropped every nested counter.
- **Generated content is INLINE CONTENT OF THE HOST**, which is what makes a `::before` share the
  host's first line. It goes into the same list the host's own text goes into, so the run-closing that
  turns mixed content into anonymous blocks applies to it for free.
- **An INLINE host reaches none of that by the obvious route.** An inline element contributes runs to
  the line being built rather than a box of its own, so it never passes through the method that
  generates content — `<q>` had no quotation marks at all until that branch got its own call, and
  `q::before` is where a browser's quotes come from.
- **The painter needs a FLAG, not a test.** It filled a run's own background only when the run had a
  selector, and generated content has no element to name one. Testing the style INSTANCE against the
  block's — the reference identity `InlineAlign` uses — looks equivalent and is wrong inside an
  anonymous block, whose style is a fresh instance while its text keeps the parent's, so every
  anonymous run painted its parent's background twice.
- **A counter's scope is its declaring element's SUBTREE**, nested inside any counter of the same name
  outside it, which is what makes `counters()` produce `1.1` and `1.2`. Popping it when the subtree
  ends is what stops a second list continuing the first's numbering. The reset applies before the
  increment, CSS's order and observable: an element doing both to one counter gives 1 rather than 0.

## Traps in shadows and rgba

`block/shadows` measures these.

- **Only an OFFSET is drawn — no blur and no spread — and the two limits have different causes.** A
  blur needs a Gaussian, which a PDF content stream cannot express for an arbitrary shape, and a text
  shadow's blur follows glyph outlines so no gradient stands in for it. A blurred shadow drawn sharp
  is a hard dark copy where a soft halo belongs, so it is not drawn at all — the rule an unsupported
  outline style already follows.
- **A spread is UNREACHABLE rather than unimplementable.** AngleSharp elides a zero blur, so
  `6px 6px 0 4px` — offset, no blur, spread four — comes back as `6px 6px 4px`, byte-for-byte what a
  real four-pixel blur comes back as. A three-length value therefore has to be read as a blur:
  reading it as a spread would draw a hard shadow wherever an author asked for a soft one, which is
  much the worse of the two mistakes. The first version of the scenario had spread rows and they
  measured wrong for exactly this reason.
- **Layers paint FARTHEST FIRST**, so the one written first ends up on top. Invisible until two of
  them overlap, which is the only time anyone writes two.
- **A shadow is behind its own background**, which is how the scenario found that `rgba()` was not
  honoured at all. `Krilla.Color` has no fourth channel — krilla models opacity as a fill property —
  so the alpha travels alongside the colour. `color` and `background-color` carry it; every other
  colour property is still drawn opaque and is reported.

## Traps in column definitions

`table/columns` measures these, exact on all sixty-three boxes.

- **`<col>` and `<colgroup>` were reported as reaching nothing**, so a table sized entirely through its
  column definitions — how reporting tools and mail merges write one — got automatic widths. The
  widths ride on the table as a positional list, since a column definition generates no box.
- **The defect was invisible in one of the two forms.** A `<colgroup span="2">` with no children
  worked immediately and every `<col>` was ignored, because the branch recognising a column definition
  returns before the ordinary child walk — so the `<col>` elements were never visited.
- **Static mutable state raced.** The pending widths lived in a static field on the box builder, which
  is static and reached by concurrent conversions: `BoxFidelityTests` walks the corpus in a loop while
  `CorpusTests` runs it in parallel, and the two reported different geometry for the same scenario
  from the same reference. They live on `DocumentContext` now, beside the counters.
- **A surplus among all-pinned columns is shared in proportion to the WIDTHS; a shortfall in
  proportion to what each column can GIVE UP** — its width less its min-content width. Four tenths of
  a pixel apart on the measured arrangement, and the whole of what separates the two readings.
- **A declared column width does not raise the floor under a table that declares its own width**, and
  does raise it for a table that shrink-wraps. Giving both cases the same answer broke one or the
  other: `table/columns` caught the first and `table/spans` the second.
- **The division residue goes to the LAST column, on Chrome's 1/64 pixel grid.** Two identical columns
  come out 83.28 and 83.31 — three hundredths apart, because the second is handed what the division
  left over. Sharing it evenly leaves them identical.
- **A `<col>` has a rectangle a browser reports** — its column's extent by the height of the row area
  — so it has to be reported here too, or every column definition counts as an element this engine did
  not produce.

## Traps in SVG

`image/svg` measures these, exact on geometry and pixel-identical to Chrome on both its pages.
`krilla-svg` does the drawing, so the engine's share is deciding how big the picture is and where
it goes — which is where all five of these live.

- **The `svg` cargo feature was on by default and exported NOTHING**, for as long as it had
  existed. Nothing in `krilla-capi/src` named `krilla_svg`, no managed P/Invoke reached it, and the
  crate was pulled in for zero functionality — while `resvg`, `usvg`, `fontdb` and `tiny-skia`
  stayed in the shipped `THIRD-PARTY-NOTICES.md` and in `deny.toml`'s advisory ignores, because
  `cargo about` and `cargo deny` read the dependency graph rather than the linked output. The size
  comment beside the feature claimed it was the biggest contributor to the binary, and measuring
  said otherwise: turning it off saved 2,560 bytes, because with nothing calling in, LTO stripped
  the lot. Wiring it up made the claim true — 4,347,392 bytes to 6,118,912 on win-x64, about
  1.7 MB on each of the eight RIDs. **A dependency's cost is a property of something calling it**,
  which is worth knowing before trusting any size note written next to a feature flag.
- **usvg's stock `<image href>` resolver READS FILES OFF DISK.** Its `resolve_string` joins a
  non-data href to `resources_dir` — the process's working directory when that is unset, as it is
  here — and embeds whatever it finds. An SVG is content and frequently comes from somewhere
  untrusted, so shipping that would have put an arbitrary file read, and then an exfiltration
  primitive, behind an `<img src="x.svg">`. `hardened()` replaces the resolver with one that
  resolves nothing and leaves the data resolver alone, which is the rule `ImageStore` already
  follows: a `data:` URI's bytes are already in the document.
- **A test for that has to name something usvg would ACCEPT.** The first version pointed the href
  at the crate's own `Cargo.toml` and asserted its bytes were absent from the output — and passed
  with the resolver fully live, because usvg sniffs the format first and drops anything that is not
  an image. Both tests are differential now: two documents differing only in whether the file the
  href names exists, so identical output is the assertion. Verified by removing the hardening and
  watching them fail.
- **An SVG with only a `viewBox` has an aspect ratio and NO intrinsic size.** SVG's own
  specification defaults the root `width` and `height` to `100%` rather than leaving them absent,
  so what looks like an absent size is a percentage — and it resolves against the containing block.
  Chrome makes such an image 816 wide at the top level and 400 inside a 400px paragraph: the full
  content width, inline and block-level alike. Reading the viewBox extent as a size instead gives
  40, and CSS 2.1's rule for a replaced element with no intrinsic width gives the default object
  size's 300; both are plausible and both are wrong. `ImageData.HasIntrinsicSize` is the one bit
  that distinguishes them, and it is the only thing about a vector image the layout engine has to
  know — `Ratio` means the same for both kinds.
- **The size read here and the size usvg resolves have to AGREE.** Layout computes a destination
  rectangle from `SvgHeader`'s answer while krilla-svg scales the tree from usvg's, so a
  disagreement fills a correctly-drawn picture into a wrongly-sized box. That is why `SvgHeader`
  mirrors usvg's resolution — including its 100x100 fallback for a document declaring neither a
  size nor a viewBox, which is not the 300x150 a browser would give — rather than implementing the
  better rule and letting the two drift.
- **A block-level replaced element is UNBREAKABLE, and nothing had noticed.** A page break landed
  inside a picture and drew its top on the page before. Invisible until an image grew close to a
  page tall, and every image in the corpus before this was a 64x32 swatch. It is the same thing to
  the slice as a table row or a `break-inside: avoid` box, so it was one condition in
  `Paginator.Unbreakable` — and all 119 existing scenarios stayed identical, which is what says the
  case really was unreachable rather than merely unmeasured. The INLINE case is not fixed: an
  inline image taller than a page goes through the line breaker rather than through
  `Paginator.Unbreakable`, and is still sliced at the page edge where Chrome moves it whole.

## The diagnostic table is only as good as its audit

`UnsupportedCss` reports what the engine reads and does not honour, and the invariant it carries — **a conversion that reports nothing laid out every construct in the document the way a browser would** — is false the moment a property is neither read nor listed. Five were found the first time, by diffing what `StyleResolver` reads against what the table lists rather than by anything failing: `min-height`, `max-height` and `text-indent` were implemented in response, and `box-shadow` and `caption-side` were added to the table. Re-run that audit when adding properties; nothing fails on its own if an entry is missed, which is exactly the problem.

The audit is a two-line shell pipeline — every property name `StyleResolver` reads, against every string `UnsupportedCss` mentions — and the difference is the properties the engine claims to honour. Reading that list is the work: each entry has to be *honoured for every value it takes*, not merely read. Two passes have each found two, and none of the four failed anything.

The most recent found a value-by-value gap in two of the properties added alongside it: an `aspect-ratio` given the two-value `auto <ratio>` form resolves to nothing, and a `background-position` or `object-position` given the four-component `right 10px bottom 5px` form had its first two components read positionally — a plausible answer in the wrong place, which is the worst kind. Both are reported now.

The pass before found:

- An unrecognised **`list-style-type`** silently became a disc. `lower-greek`, `armenian`, `georgian` and the CJK styles all came out as bullets with no report — the same shape as the `display` fallback, which *is* reported, and the source comment even acknowledged the fallback without anyone noticing the missing entry.
- **`white-space: break-spaces`** silently inherited. It preserves white space and wraps like `pre-wrap`, differing only in that a run of trailing spaces may itself be broken.

Both are now reported, and the lesson generalises: a value-by-value fallback with a reasoned comment beside it is exactly where an unreported gap hides, because the comment makes the code look considered.

The other thing the audit cannot see is a syntax the resolver does not PARSE. `calc()` was the case: it fell through to the unparseable fallback, the property took its default, and no diagnostic could fire because nothing recognised the value as one the engine was getting wrong. **A value nothing recognises is a value nothing can report.**

Still reported rather than implemented: `column-count`, `writing-mode`, `direction`, `font-variant`, `font-stretch`, a wavy text decoration, `word-break: keep-all`, a blurred or spread shadow and an `inset` one, `rgba()` on a border, an outline or a decoration, `break-before`/`break-after: avoid`, automatic hyphenation, a `position: fixed` box with neither `top` nor `bottom` given, `visibility: collapse` on a table row, an unrecognised `list-style-type`, `white-space: break-spaces`, an unresolved `list-style-image`, a `content` value that names something unreadable, a non-inline `display` on a pseudo-element, a named `@page` selector, rounded corners on an inline element, and a gradient as an inline element's background.

Two entries came OFF that list by being measured rather than implemented, which is its own kind of result. `border-style: hidden` inside a collapsed table was documented as unimplementable — the width was folded to zero before anything could tell it from an absent border — and became a two-line change once `hidden` was kept as a style of its own; `table/collapse` is pixel-identical with it. And `visibility: collapse` on a table row was written, measured, and reverted: Chrome disagrees with ITSELF, its screen layout zeroing the row's track while its printed page puts everything after it twenty pixels further down, so no engine behaviour can be exact on both of the corpus's measurements.

One difference is deliberately NOT reported: Chrome interrupts an underline around a descender (`text-decoration-skip-ink`, default `auto`), which needs glyph outlines rather than advances. It is a default rather than a declaration, so a report would fire on every underlined document ever converted. `text/decoration_style` records it as a named residual instead — which is the right home for a difference that no author asked for.

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
- **The showcase specimen's page baseline is compared by SSIM, so it can drift without failing.** `VerifierSettings.UseSsimForPng()` applies to it as well as to the corpus, and a change confined to one inline element passed as "equal" while the committed PNG — the image the readme shows — no longer matched the render. The corpus is the real gate; when a change alters the specimen, delete `ShowcaseTests.Specimen#page_0001.verified.png` and let it be regenerated rather than trusting the comparison to notice.
- **A block background fill is SNAPPED to whole pixels, and so are an inline fill and a background image.** All three because that is what the browser fills; the block one was found last, by `aspect-ratio` producing the corpus's first deliberately fractional box height. Applying it improved eight existing scenarios and regressed none. Snapping in LAYOUT units does not guarantee whole DEVICE pixels, though — coordinates round-trip through PDF points, so a fractional width can still leave a faint extra column, which is why `PageRuleTests` asserts a measured box rather than where the ink stops.
- **`BaselineHealthTests`' degeneracy threshold is 1, not Morph's 16.** Morph reasons that a rendered page always carries anti-aliased text and so has hundreds of colours. This corpus deliberately contains flat-fill scenarios with three colours total, precisely so they carry no rasterisation noise — anything above two fails them. The guard is correspondingly narrower, which it can afford to be because every page here is also compared against a browser reference.

## Package Management

Central Package Management; versions live in `src/Directory.Packages.props`. Rust dependencies are pinned exactly (`=0.8.2`) because the test suite compares PDF bytes, so a patch bump is a deliberate, baseline-regenerating change.
