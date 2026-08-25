# page/fixed_repeat

# page/fixed_repeat

`position: fixed` on a document long enough to paginate.

CSS 2.1 §9.6.1 says a fixed box in paged media is repeated on every page, and Chromium's printer
agrees: all three boxes here appear at the same place on each of the three sheets. That is the
whole of what this scenario measures, and it can only be measured in PIXELS — the geometry harvest
runs against one continuous layout, so the browser reports each of these boxes exactly once, and a
converter that drew them on page one alone would match the box comparison perfectly.

`#header` and `#footer` are the two halves of the rule. A `top` offset is indistinguishable from
an absolute box anchored to the start of the document, since the two coincide on page one;
`bottom: 0` is not, because measured against the document it would land after the last paragraph
on the last page and appear nowhere else. `#tab` sits at neither edge, so its position is
arithmetic rather than somewhere it could have arrived at by accident.

## Measured, and deliberately not matched

A fixed box with `top` and `bottom` both auto sits at its STATIC position, and Chromium's printer
does something with it that is not worth reproducing: it draws the box once where flow put it —
across a page boundary if that is where it falls — and then ALSO repeats it, at that same
page-relative offset, on every later page but on no earlier one. A box straddling a break
therefore appears twice on the page after it.

This engine paints such a box once, where flow put it, which is what it did before repetition
existed. The alternative is worse than the divergence: a static position is a position in the
DOCUMENT, so repeating it would add each page's own top to a coordinate that already includes it,
and a box whose flow position is on page three would fall off the bottom of every page and vanish
from a document it currently appears in. No row here carries one, because a corpus scenario cannot
record a difference it has decided not to match.

**Boxes**: 19 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0039 · SSIM 0.9992** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |
| **Page 2** | **Page 2. AE 0.0015 · SSIM 0.9994** |
| <img src="reference_0002.png" width="480"> | <img src="result%23page_0002.verified.png" width="480"> |
| **Page 3** | **Page 3. AE 0.0012 · SSIM 0.9998** |
| <img src="reference_0003.png" width="480"> | <img src="result%23page_0003.verified.png" width="480"> |

