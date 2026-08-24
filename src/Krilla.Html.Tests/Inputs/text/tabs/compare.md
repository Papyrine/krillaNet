# text/tabs

# text/tabs

Pixel-identical to Chrome, and geometry-exact.

A tab under `pre` advances to the next tab stop rather than to a width of its own, so it is the one
token in the engine whose width is not known when it is measured: where the stop falls depends on
how far along the line the tab already sits. The token carries the STOP SPACING instead, and the
advance is settled while the line is being filled.

Measured out of Chrome across three values of `tab-size`, with Liberation Mono at 16px giving a
9.6026px advance:

- The stops sit at multiples of `tab-size` SPACE ADVANCES from the start edge of the line. At the
  default 8 that is 76.82px, at 4 it is 38.41, at 2 it is 19.20, and every span in the scenario
  lands on one.
- A tab already sitting exactly on a stop advances to the NEXT one rather than to nothing. `a` is
  one character wide and its tab reaches 8, not 1; a leading tab on an empty line reaches 8, not 0.
- Nine characters followed by a tab reach 16 rather than 12, which is what makes the second line of
  each block a different arithmetic from the first.

Two things had to be kept away from the shaper. A tab is a real character in the text of its item,
so it carries a shaped range like any other token, and a run merges with its neighbours on exactly
that contiguity. Left to merge, the tab CHARACTER was drawn as a glyph, at the advance of that glyph
rather than at the distance to the stop, which moved every glyph after it inside the same run. It is
also excluded from the unbreakable-run widths, where its width field means a stop spacing rather
than an advance and summing it measures nothing.

A `tab-size` given as a LENGTH is reported rather than honoured.

What to look at: the x of every span. Each should be an exact multiple of the stop of its block.

**Boxes**: 14 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

