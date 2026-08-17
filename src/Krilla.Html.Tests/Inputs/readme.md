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

Both currently sit at zero across the corpus: every scenario matches Chrome's geometry exactly, and
24 of 25 are pixel-identical. The exception is `block/borders` at SSIM 0.9997, whose residual is the
four un-mitred corners that scenario exists to expose.

The pixels reach *exactly* identical because the reference is printed rather than screenshotted, so
both sides are rasterised by PDFium. A screenshot would put Skia on one side and PDFium on the
other, and two rasterisers disagree about glyph edges however correct the layout is — a floor
somewhere around 0.90–0.97 on any page of text. That is the trade the printed reference buys, and
it is why a regression here is unambiguous rather than lost in noise.

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

## reset.css

Prepended to every scenario and to the browser's copy. It removes the differences that are not the
one being tested: the two engines' default stylesheets, the shaping features the text measurer
cannot do, and the base font binding. Its header comment explains each in detail.

Changing it invalidates every committed reference, so regenerate them all afterwards.

## A warning about regenerating

`Krilla.Html.RefGen` is explicit and lives in a separate project on purpose. A reference regenerated
during a test run would move the target to wherever the render landed, and the suite could never
fail. Regenerate after adding a scenario or changing `reset.css` — never to make a failing
comparison pass.
