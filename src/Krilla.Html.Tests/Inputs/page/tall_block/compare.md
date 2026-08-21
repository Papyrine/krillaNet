# page/tall_block

# page/tall_block

A page break can be needed where no line offers one. `Paginator` breaks before a line that would
straddle the boundary, and a block taller than the page contains no lines — so there is no
candidate inside it, the break lands after the whole block, and everything between the page edge
and the block's end is never drawn. `NextTop` falls back to breaking at the page edge for exactly
this case, and nothing measured that fallback.

`page/page_size` stops one pixel short of reaching it: its content is 1040px plus a 16px border,
which is 1056 exactly — one page, with no fragment left over. Here the block runs 0..1400, so page
one is entirely filled by it, page two carries the remaining 344px, and the paragraph follows at
y=1400, which is 344 down page two.

`#mark` is a 40px block at the top, and it is there for `BaselineHealthTests`: without it page one
is a single flat colour, which is indistinguishable from a page that rendered nothing at all. It
generates no line, so the block it sits in stays free of break candidates and the fallback is
still what produces the break.

What to look at when it moves: a second page that is blank above the paragraph means the block was
not painted past the break. A single page means the fallback did not fire at all and the overflow
was dropped.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |
| **Page 2** | **Page 2. AE 0.0004 · SSIM 0.9999** |
| <img src="reference_0002.png" width="480"> | <img src="result%23page_0002.verified.png" width="480"> |

