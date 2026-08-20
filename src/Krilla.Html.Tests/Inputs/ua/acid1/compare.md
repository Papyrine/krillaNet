# ua/acid1

Acid1, the CSS1 conformance test, from the W3C CSS1 test suite (`test5526c.htm`, dated 1998). The
only scenario here not written for this corpus, and it earns the exception: it exercises floats,
percentage widths, the box model and `clear` together, in an arrangement nobody designing a
scenario would have thought to build.

Two changes were made to the imported markup, both recorded so the import can be repeated:

- `Verdana` becomes `Liberation Sans`, since the corpus pins its fonts to the bundled faces. Text
  metrics change and no box does, which is what the test measures.
- The `<form>` and its two radio buttons are removed. Form widgets have no rendering here and the
  test itself exempts them — its own instructions say agents should match "except font
  rasterization and form widgets" — but a widget with no rendering is still a box the browser
  reports, so leaving them in would hold four boxes permanently unmatched and cost the corpus its
  property that geometry is zero everywhere.

It found two defects on its first render, neither of which any hand-written scenario had reached:

- The root element's background propagates to the CANVAS and covers the whole page rather than the
  root box alone. Acid1 paints the page blue and its content stops a third of the way down, so the error
  was two thirds of the page. `Inputs/reset.css` paints the root white over a page that was already
  white, which is why fifty-three scenarios had passed over it.
- `line-height` was not inherited, so `html { font: 10px/1 }` set the line height of the root and
  of nothing inside it. That is how nearly every stylesheet sets line spacing, and it left two
  boxes here a pixel too tall.

With both fixed it matches Chrome exactly.

**Boxes**: 14 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0009 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

