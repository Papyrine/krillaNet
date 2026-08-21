# float/margins

# float/margins

Every float rule is stated against the MARGIN box, and no other float scenario has a float with
margins on it — `float/basic` has none at all, and `float/clear`'s `#m` carries a bottom margin
only, to measure clearance. So the three places the margin box matters were unmeasured in pixels:

- **`#spaced`** — line shortening. The paragraph's lines start 20px past the float's border edge,
  and the float sits 12px in from the container's left edge rather than against it.
- **`#low`** — the flow position. A float with `margin-top: 24px` puts its MARGIN box at the flow
  position, so the border box lands 24px down. Adding the margin when choosing the position and
  again when translating the box into place lands it at 48px, which is a bug that was fixed once
  and that nothing in the corpus could have caught.
- **`#a`/`#b`** — float-against-float placement. Two 170px floats fit side by side in 400px; two
  210px margin boxes do not, so the second descends.

All three are exact, and the page reads SSIM 1.0000.

What to look at when it moves: a horizontal shift of exactly one margin on `#spaced`'s text is
line shortening reverting to the border box. `#low` at y=48 rather than y=24 is the double
application. `#b` beside `#a` rather than below it is the fit test reading border boxes.

**Boxes**: 12 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0001 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

