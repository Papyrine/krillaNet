# inline/white_space

# inline/white_space

`white-space` has five values and the engine implements all of them. `inline/white_space_pre`
measures two — `pre` and `normal` — and the other three had no coverage anywhere, in the corpus or
in a unit test.

Each of the three differs from `pre` in exactly one respect, which is what makes them worth
separating:

- **`pre-wrap`** preserves the spaces and the newline like `pre` does, but also breaks a line that
  runs out of room. Under `pre` the same text would overflow.
- **`pre-line`** collapses the runs of spaces like `normal` does, and keeps the newline. The
  leading indentation on each source line disappears; the break between them does not.
- **`nowrap`** collapses like `normal` and never breaks, so the text runs past the box's right
  edge rather than wrapping at it.

Monospace at 14px, so a column of characters is countable against the reference rather than merely
comparable.

What to look at when it moves: `#wrap` losing its interior spaces is `pre-wrap` being read as
`normal`; `#wrap` overflowing is it being read as `pre`. `#line` keeping its indentation is
`pre-line` preserving what it should collapse, and `#line` on one line is the newline being
dropped. `#nowrap` wrapping at 300px is the whole value being ignored.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0002 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

