# position/relative

Relative positioning, which moves a box without moving anything else.

- `#flow` is the point of the whole value: `#b` shifts down and right, and `#c` sits exactly where
  it would have sat had `#b` never moved. The space `#b` was given stays given. An implementation
  that treated the offset as part of layout would push `#c` down and pass a scenario containing
  only one box.
- `#corners` covers the pair that reads backwards. `bottom` and `right` name the edge the box moves
  AWAY from, so `bottom: 8px` lifts a box and `right: 30px` pulls it left. Guessing the sign here is
  a coin toss and the corpus is the coin.
- `#nested` offsets a parent and measures the child, since the shift applies to the whole subtree
  rather than to one border box.

The heights are declared rather than derived so that a wrong offset shows as a moved box and not as
a reflow, which keeps the failure legible.

**Boxes**: 12 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0001 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

