# block/list_image

# block/list_image

`list-style-image` was in the table of properties the engine does not read at all, and closing it
turned out to be a layout change rather than a painting one. The measurement said so immediately:
an item whose marker is a 32px image is **39px tall**, not 24. The marker is an atomic inline on the
item's first line, bottom edge on the baseline, so it grows the line exactly as an inline image of
that height does — 32 above the baseline plus the strut's 7 below.

That is why the image is prepended to the item's own inline content rather than handed to
`ListMarkers` like a symbol or a counter. A marker drawn beside the item could not have grown it.

The two placements differ only in the advance they take, and both numbers were measured:

- **`outside`** takes none. The line starts where it would have with no marker, and the image is
  drawn back beyond that, its right edge held clear of the item's border edge by the same **seven
  pixels** a symbol marker leaves. It is the same constant, because it is the same gap — checked by
  putting `#symbol` in the scenario and finding its bullet's right edge at the same place.
- **`inside`** takes its own width PLUS that gap. Reading it as the width alone is off by seven
  pixels, and the pixels invite that mistake: the item's background becomes visible
  exactly at the image's right edge, so the image looks like the whole advance until the text is
  measured instead of the fill.

`#missing` names an image that does not resolve, and Chrome draws the `list-style-type` behind it —
a square here. So the fallback is on the RESOLVED image rather than on the declaration, which is
also what makes the counter style worth keeping in the box tree at all.

An item whose entire content is a block has no line here to hang a marker from, and keeps the
counter marker it would have had. That is a limitation rather than a rule: a browser puts the marker
on the first line wherever it is, including inside a nested block.

**Residual**: SSIM 0.9999, and every differing pixel is a glyph edge or the antialiasing of the
disc in `#symbol` — the marker positions and the item heights are exact.

What to look at: the item heights, which the geometry comparison pins at 39px, and where the text
starts in `#inside`.

**Boxes**: 11 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0006 · SSIM 0.9999** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

