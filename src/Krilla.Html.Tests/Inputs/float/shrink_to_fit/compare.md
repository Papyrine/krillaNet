# float/shrink_to_fit

How wide a float with no declared width becomes, which CSS 2.1 §10.3.5 gives as
`min(max(min-content, available), max-content)`.

The three terms of that formula each need a case to be distinguishable, and this has one for each:

- `#short` is narrower than the container, so it takes its max-content width — the width of its
  text with no wrapping at all. This measures the text advance itself, not merely the algorithm.
- `#long` wants more than 400px, so it takes what is available and wraps inside itself. An
  implementation using max-content unconditionally overflows here.
- `#word` is a single unbreakable word inside a 120px container. Neither term of the outer minimum
  can go below min-content, so the float overflows its parent rather than breaking the word.
- `#overflow` is wider than the container outright, so it hangs out to the right AND leaves the
  paragraph beside it no band at all. That paragraph descends below the float instead of drawing
  its line in zero width, which is the CSS 2.1 §9.5 rule for a line box shortened to nothing.

The last is worth the scenario on its own: a shortened line and a line with nowhere to go are
different code paths, and only one of them is exercised by every other case here.

**Boxes**: 14 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

