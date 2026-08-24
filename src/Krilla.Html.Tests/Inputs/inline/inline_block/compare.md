# inline/inline_block

# inline/inline_block

`display: inline-block` used to lay out as a block and report a diagnostic, so a row of anything
built out of it became a column. It is now an atomic inline: one unbreakable box on a line, with
contents laid out as a block in a formatting context of its own.

What separates it from the other atomic inline the engine already had is the baseline, and three of
these four arrangements are about that.

- **`#baseline`** — one line of content. Its baseline is its own line's, so the text inside it and
  the text beside it sit on the same baseline. An image would put its bottom edge there instead.
- **`#two`** — two lines of content. CSS 2.1 §10.8.1 takes the LAST line's baseline, so the box
  hangs upward and the text beside it lines up with its second line. Taking the first line's
  instead is the plausible reading, and puts the box a whole line too low.
- **`#empty`** — no in-flow line box at all, which is where the rule falls back to the bottom
  MARGIN edge. That is the image case, reached from the other direction.
- **`#model`** — padding, border and margins on all four sides. The horizontal margins hold the
  text away exactly as the border box does, which is why the token measures the margin box rather
  than the border box; the vertical ones do not collapse with anything, the box establishing its
  own formatting context.

What to look at when it moves: any of the four boxes on a line of its own is the atomic-inline
path being lost entirely. `#two` a line too low is the first-line baseline. `#baseline` sitting a
few pixels low with its text still aligned is half-leading, not this.

**Boxes**: 11 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0002 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

