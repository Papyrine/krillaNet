# block/outline

# block/outline

An outline is a border that takes no space. It is drawn outside the border edge and moves nothing,
which is what makes it the usual choice for a focus ring — a border there would shift the page
every time focus moved.

That is the whole of what the first and last rows measure together: `#plain` carries a 3px outline
and `#neighbour` sits exactly where it would have without one. The geometry comparison is what
catches a regression there, since an outline that took space would move every box below it.

`#offset` pins the offset's meaning. A 3px outline at an offset of 4 on a box starting at y=38
paints rows 31 to 33 — so the gap between the box and the ink is the offset, and the ink is
entirely outside it. Reading the offset as moving the ring's centre rather than its inner edge is
off by half the width, which is a pixel and a half here and invisible until it is measured.

`#both` puts an outline against a real border, so the two rings are distinguishable and their order
is visible: red at 73-75, green at 76-79, background from 80.

The ring is drawn as one path rather than four edges, for the reason a uniform border is: two
antialiased edges meeting on a mitre diagonal do not composite to full coverage. An outline is
uniform by construction — one width and one colour on all four sides — so the question never
arises.

Only `solid` is drawn. The other styles zero the width and report, rather than painting solid the
way an unsupported BORDER style does. The two differ because an outline is decoration with no
layout consequence: drawing the wrong ring is a worse answer than drawing none, where for a border
the box has already reserved the space and something has to fill it.

**Residual**: SSIM 0.9999, and every differing pixel is in a row of text rather than in a ring —
the corpus's usual sub-pixel glyph positioning. The outlines themselves are exact.

What to look at: the position of `#neighbour`. If it has moved, the outline is taking space.

**Boxes**: 6 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0006 · SSIM 0.9999** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

