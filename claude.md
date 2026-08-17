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

Three stages, each inspectable on its own: AngleSharp parses and runs the cascade, `Layout/` turns the styled tree into positioned boxes, and `Painting/PdfPainter` draws those boxes through `Krilla.Surface`. Implemented: block and inline layout, the box model, collapsing margins, line breaking, alignment, pagination, and images. Floats, positioned boxes, flex, grid and tables lay out as plain blocks — deliberately, so unimplemented CSS shows up in the corpus as a geometry difference rather than as missing content nothing measures.

Three structural points worth knowing before changing anything:

- **Layout never touches the native.** `FontFace` loads krilla's `Font` lazily, and `ImageData` reads its size from the file header rather than from `PdfImage`, so measuring runs entirely in managed code and krilla is reached only when a page is painted. That is what lets `BoxFidelityTests` — the primary fidelity gate — run on a machine with no Rust toolchain, where every PDF-producing test cannot run at all.
- **Nothing applies its own vertical margins.** `BlockLayout.Layout` is handed the top of the border box; the caller consults `LeadingMargin` first. Margins collapse through nesting, so the margin above a box may have come from a grandchild, and only the ancestor placing it can know the final value.
- **Images do not fetch over the network**, and that is a security default rather than a missing feature — converting an untrusted document would otherwise issue requests to whatever hosts it names. `HtmlOptions.ImageResolver` is where a caller takes that decision explicitly, with whatever timeout and host allow-list applies.

### The corpus (`src/Krilla.Html.Tests/Inputs`)

One directory per scenario, holding `input.html`, `input.css`, `notes.md`, and the committed browser reference: `reference_0001.png` and `reference.boxes.json`. Structure borrowed from `Morph/src/Tests/Inputs`, which solved the same problem for DOCX.

It records two independent measurements, and **asserts neither**:

- **`reference.boxes.json`** — the browser's `getBoundingClientRect()` per element, against our box tree. Integer-exact, localising ("this paragraph is 14px low" is a defect report), and — the practical reason it leads — computable without the native library, so it works on a machine with no Rust toolchain.
- **`reference_0001.png`** — pixels, via AbsoluteError and SSIM.

**Both currently sit at zero.** Every scenario matches the browser's geometry exactly, and 24 of 25 are pixel-identical (SSIM 1.0000, AE 0.0000). The exception is `block/borders` at 0.9997, whose residual is the four un-mitred corners that scenario exists to expose.

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

- **`ComputeCurrentStyle` resolves percentages against the render device's viewport.** `width: 25%` inside a 600px container comes back as `204px` — a quarter of the *page*. A percentage resolves against a containing block, which is a layout result AngleSharp cannot know. So `StyleResolver` reads `IStyleCollection.ComputeCascadedStyle` instead, which leaves `25%` and `0.5em` as written, and `CssLength` carries them into layout to be resolved where the answer exists. Do not "simplify" this back to the computed style.
- **AngleSharp compares specificity across cascade origins.** CSS resolves origin *before* specificity, so an author `* { margin: 0 }` beats a UA `p { margin: 1.12em 0 }` outright and a browser zeroes it. AngleSharp lets the UA rule win on specificity, so `body` keeps its 8px margin and every paragraph keeps a 17.92px one. `Inputs/reset.css` enumerates element names to match the UA selectors' specificity. This is a real limitation for consumers too, not just a corpus quirk.
- **AngleSharp has no `display` for the inline elements.** It reports an empty string for `b`, `i`, `span` and the rest. Reading that as `block` puts every piece of emphasised text on a line of its own, which is a whole-paragraph error from a missing default. `Styling/UserAgentStyles.cs` supplies them.

## Traps in text layout

Three of these were found the same way: a scenario sat at SSIM ~0.93 while its box geometry was exact, meaning layout was right and painting was wrong. That combination is the signature of a rasterisation-level bug, and it is worth recognising.

- **Half-leading must be FLOORED, and the descent derived by subtraction.** With integer ascent and descent the exact half lands on .5 constantly — 16px text in a 24px line gives 3.5 — and a baseline at 17.5 rasterises one whole pixel lower than one at 17. Every glyph in the corpus was a pixel low until this was floored. `Below` is then `lineHeight - Above` rather than a second floor, or each line loses a pixel and the drift compounds into a wrong page count.
- **`line-height: normal` needs the font's metrics rounded to whole pixels *before* summing.** Liberation Sans at 16px gives 14.48 + 3.39 + 0.52, which is 18.4 unrounded but 14 + 3 + 1 = 18 the way a browser does it. Four tenths of a pixel per line is invisible on one line and a whole line of drift down a page. This is the one place the engine imitates a specific implementation rather than a specification — CSS defines `normal` as UA-defined, so there is no correct value to compute and agreeing with the reference browser is the useful choice.
- **A page ends where the next one begins, not at the bottom of the paper.** `Paginator` moves a straddling line whole to the next page, so the last line on a page can end well short of the sheet. `PdfPainter.Paint` therefore takes `pageEnd` separately from the page box: painting down to the paper instead draws that line here clipped in half AND again overleaf in full.
- **Leading white space is only trimmed where white space collapses.** Under `pre` the indentation *is* leading spaces, and applying the `normal` trimming rule left-aligns every deliberately indented line.

## Traps around replaced elements

- **`<img>` is inline-level.** Defaulting it to block puts a picture on a line of its own, so an image mid-sentence drops below the paragraph text. It lives in `UserAgentStyles`' inline set alongside `<b>` and `<span>`, despite being replaced rather than textual.
- **A replaced box is never self-collapsing.** `IsSelfCollapsing` tests for a zero height, and an image sized from its aspect ratio has `height: auto`, which reads as zero — so without an explicit exclusion the image's own bottom margin collapses through it and pushes it down by that margin.
- **An atomic inline sits its bottom edge on the baseline**, so a tall image pushes the line's top upward rather than growing it downward. That is what `vertical-align: baseline` means for a replaced element, and it is why an image taller than the line still leaves the text where it was.
- **Clamping a width has to rescale an auto height.** `max-width: 100%` on a photograph in a narrow container must shrink both dimensions; rescaling only the width is how images end up distorted in responsive layouts. `ReplacedSizing` does it, and `image/max_width` exists to catch a regression.
- **Corpus options are per scenario, not shared.** `CorpusRunner.Options(directory)` sets the base URL a relative `src` resolves against. Calling the parameterless overload for a scenario silently drops every image and reports the absence as a layout difference — which is exactly how a wrong `BoxFidelityTests` baseline nearly got promoted.
- **`OS/2` bit 7 (`USE_TYPO_METRICS`) changes which vertical metrics win.** When set, `sTypo*` beats `hhea`. Browsers honour it, so `OpenTypeMetrics` does too; ignoring it puts every line box a few percent out, which compounds into a wrong page count.
- **Text is measured by summing raw `hmtx` advances** — no kerning, ligatures or complex-script shaping, because krilla exposes no shaping and adding a second shaper alongside its rustybuzz would be the wrong fix. `Inputs/reset.css` disables the features a browser would apply, which makes the two sides' advances identical rather than approximately so. The real fix is exposing rustybuzz through krilla-capi; until then, corpus text stays inside what this can measure.
- **Whitespace-only inline content generates no box.** The newline between two block elements is collapsible whitespace, and wrapping it in an anonymous block gives every indented document a blank line before each section.

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
