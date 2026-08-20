# position/anchors

Which box an absolute box is positioned against, in the three arrangements that differ.

- `#deep` is two levels down, inside a `#static` carrying a 30px margin, and anchors to `#outer` —
  skipping the static parent entirely. Its margin, its position and its padding contribute nothing.
  This is what makes `position: relative` on an outer element the standard way to anchor something
  nested, and an implementation that walked to the DOM parent would land 30px out in both axes.
- `#half` measures which dimension a percentage offset resolves against, and the answer is not one
  of them: `left` resolves against the containing block's WIDTH and `top` against its HEIGHT. A
  single square container cannot tell those apart, so `#pct` is deliberately not square.
- `#page` has no positioned ancestor at all, so its containing block is the page rather than the box
  that declares it. It is written inside a box most of the way down the page and lands near the top,
  which looks like a bug until the rule is known.

**Boxes**: 9 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

