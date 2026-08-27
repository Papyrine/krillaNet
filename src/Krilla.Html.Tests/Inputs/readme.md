# The Krilla.Html corpus

One directory per scenario, grouped by the CSS area it exercises. Each scenario isolates a single
feature, so a failure names the feature rather than pointing at a page that looks wrong somewhere.

## What a scenario holds

| File | Committed by | Purpose |
| --- | --- | --- |
| `input.html` | hand | The markup. Inlined into a document with `reset.css` and `input.css`. |
| `input.css` | hand | The rules under test. |
| `notes.md` | hand | What this scenario isolates, and what to look at when it moves. Surfaced in `compare.md`. |
| `reference.boxes.json` | `Krilla.Html.RefGen` | Chrome's `getBoundingClientRect()` for every element. |
| `reference_0001.png` | `Krilla.Html.RefGen` | Chrome's printed page, rasterised by PDFium at 96 DPI. |
| `result.verified.json` | test run | The recorded comparison: box differences and pixel metrics. |
| `result#page_0001.verified.png` | test run | The page Krilla.Html produced. |
| `result.verified.pdf` | test run | The PDF it came from. |
| `compare.md` | test run | The two side by side, with metrics. |

## The two measurements

Neither is asserted against a threshold. Both are snapshotted, so a change has to be looked at and
accepted — which makes an improvement and a regression equally visible, where a threshold only
reports crossings.

**Box geometry** is integer-exact, localising — `p:nth-child(3) dy=14` says which element moved and
by how much — and computable without the native library, so it works on a machine with no Rust
toolchain. Zero means the box tree agrees with Chrome exactly.

**Pixels** are AbsoluteError and SSIM against the printed reference.

Boxes sit at zero across the whole corpus: all 153 scenarios match Chrome's geometry exactly. Pixels
are close behind — 105 read SSIM 1.0000 and 74 are identical outright.

Several defects reached that state rather than starting there, each found by the scenario named for
it: `block/anonymous` hoisted trailing inline content above a block sibling, `position/fixed`
resolved a fixed box against the nearest positioned ancestor instead of the page, `table/empty`
applied edge spacing to a section with no columns, `page/table_break` broke at the line inside
a table row rather than at the row's edge while leaving a sliver of the moved row behind,
`inline/vertical_align_length` could not see an inline element at all, `text/word_break` moved a
long word to a fresh line and then never offered it for splitting, and `position/z_index` painted an
absolute box inside a stacking context twice. Each scenario's `notes.md` records what it found and
what changed.

The forty-eight below 1.0000 come down to named causes. Four of them are the BROWSER's:
`table/cell_baseline` (0.9926), where Chromium's printer reserves the taller row that cell baseline
alignment demands and then leaves the content against the top of it; `page/tall_image` (0.9750),
where it drops the margin above the paragraph after an overflowing picture;
`block/background_repeat` (0.9842), where it draws a spaced background through a filtered shader and
smears every tile edge across two pixels; and
`block/translucent`'s high `AE`, which is a one-unit rounding difference in alpha compositing. In
all four the box comparison is exact, which is what says the disagreement is with the printer
rather than with the layout. The rest are sub-pixel glyph positioning, which `text/kerning` exists
to measure, box edges landing on fractional pixels, which `position/absolute` and
`image/inline_flow` measure, and a vertical dotted border edge in `block/border_styles`. SSIM
1.0000 is not quite the same as pixel-identical, which is what the `AE` column is there to show.
Each scenario's `notes.md` names its own residual.

The pixels reach *exactly* identical because the reference is printed rather than screenshotted, so
both sides are rasterised by PDFium. A screenshot would put Skia on one side and PDFium on the
other, and two rasterisers disagree about glyph edges however correct the layout is — a floor
somewhere around 0.90–0.97 on any page of text. That is the trade the printed reference buys, and
it is why a regression here is unambiguous rather than lost in noise.

Two kinds of element are left out of the harvest, and both for the same reason: they generate no box
here, so counting them would count an absence as a failure. An element with `display: none` is one;
everything INSIDE an `<svg>` is the other, since the whole subtree is drawn by krilla-svg and the
browser reports a rectangle for every shape in it.

## Adding a scenario

1. Create the directory under a category, with `input.html`, `input.css` and `notes.md`.
2. Keep it to one feature. A scenario testing three things reports one number for all three.
3. Set `line-height` explicitly unless the scenario is specifically about `line-height: normal` —
   that one value depends on font metrics and would otherwise contaminate every other measurement.
4. Regenerate the reference:
   ```
   dotnet run --project src/Krilla.Html.RefGen -- --treenode-filter "/*/*/ReferenceGenerator/*"
   ```
5. Run the category and accept the new snapshots.

## The two shared stylesheets

`reset.css` is prepended to every scenario and to the browser's copy. It is what makes the two
engines comparable at all: it pins the fonts both sides load. It used to disable kerning and
ligatures too; shaping removed that concession, which is what lets the `text/` category measure
them.

`flatten.css` follows it, and removes the default stylesheet from the picture so a scenario measures
the layout algorithm rather than the defaults table. A scenario opts out by containing a file named
`no-flatten` — the `ua/` category does, because those scenarios exist to measure exactly what
flattening would remove.

Changing either invalidates every committed reference, so regenerate them all afterwards.

One limit worth knowing: a generic font family cannot be pinned, because the reference generator
binds real family names via `@font-face` and a generic is not legal as an `@font-face` family. So
`reset.css` names `Liberation Mono` on the monospace elements directly, and "does `<pre>` default to
monospace" is a question this corpus cannot ask.

## A warning about regenerating

`Krilla.Html.RefGen` is explicit and lives in a separate project on purpose. A reference regenerated
during a test run would move the target to wherever the render landed, and the suite could never
fail. Regenerate after adding a scenario or changing `reset.css` — never to make a failing
comparison pass.
