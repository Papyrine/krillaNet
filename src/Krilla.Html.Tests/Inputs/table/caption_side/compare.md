# table/caption_side

# table/caption_side

Pixel-identical to Chrome. Two tables differing in one declaration, so what is measured is the
property rather than the look of a caption.

A caption is a block laid out at the table's own width, above or below the grid, inside the table's
box. The measurement that matters is the gap: Chrome puts a bottom caption exactly as far under the
last row as a top one sits above the first, and that gap is the table's own edge spacing rather
than anything the caption carries. So the bottom caption goes after the grid's trailing edge
spacing, which the grid had already added — the alternative, adding a gap of its own, doubles it.

`#top` states the default explicitly. It is there so the two rows differ in exactly one
declaration, which is what makes a difference between them attributable.

What to look at: the 2px band between the caption and the grid in each table. Four pixels in either
one is the edge spacing being applied twice.

**Boxes**: 14 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

