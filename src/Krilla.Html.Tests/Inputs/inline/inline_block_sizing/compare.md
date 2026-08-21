# inline/inline_block_sizing

# inline/inline_block_sizing

The other half of `inline/inline_block`: how wide an atomic inline gets, and what a line does with
a row of them.

- **`#fits`** — two boxes written with no white space between the tags, so there is no space
  between them on the page either. Each is shrink-to-fit at its max-content width.
- **`#spaced`** — the same two with a newline between the tags, which collapses to one space and
  separates them. It is the difference behind every `font-size: 0` hack in the wild, and it is
  measurable rather than a curiosity.
- **`#wraps`** — three 90px boxes with 8px margins. 294 fits and 392 does not, so the third moves
  to a second line: a line breaks before or after an atomic inline and never inside it.
- **`#squeezed`** — content wider than the container. Shrink-to-fit is
  `min(max(min-content, available), max-content)`, the same rule `BlockLayout.ShrinkToFit` gives a
  float, so the box takes the whole 300px and wraps inside itself rather than overflowing.

What to look at when it moves: `#fits` and `#spaced` rendering identically means white space
between the boxes is being dropped or invented. `#wraps` on one line means the box is being
measured to its border box rather than its margin box. `#squeezed` overflowing means shrink-to-fit
took the max-content width without clamping to what was available.

**Boxes**: 14 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0002 · SSIM 0.9999** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

