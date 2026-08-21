# float/mid_line

# float/mid_line

A line is shortened by the floats its box OVERLAPS, not by those under its top edge. The two
readings agree on every arrangement where floats begin and end on line boundaries, which is nearly
all of them, and this is the arrangement where they do not.

`#early` occupies y=0..10 and is 60 wide. `#late` clears it, so it starts at y=10 — inside the
first line box, which runs y=0..24 — and it is 220 wide. The first line therefore has 180px to
work with, from x=220 to the container's right edge. Sampling the band at the line's top edge
alone sees only `#early` and reports 340px, which fits roughly twice as many words.

This is the case `FloatGeometryTests` keeps as `e1`, which is the only one of its nineteen
arrangements that distinguishes the two readings. It had no pixel measurement until now: this
scenario is that case as a rendered page, so the word count on the first line reports it directly.

Measured, the page is pixel-identical to the reference: the band reading is right, and this is
now the confirmation of it in pixels rather than in a probe case.

What to look at when it moves: the first line carrying more words than the second is the top-edge
reading. The lines below the float are unaffected either way, which is what makes the first line
the whole signal.

**Boxes**: 6 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

