# position/absolute

Absolute positioning against a nearest positioned ancestor.

`#frame` carries a 5px border and 10px padding so that the containing block is identifiable rather
than merely plausible. The containing block is the PADDING box: `#tl` at `top: 0; left: 0` lands
inside the border and outside the padding. Using the border box puts it 5px out in both axes and
using the content box puts it 10px in, and a frame with neither border nor padding cannot tell the
three apart — which is why this scenario has both.

- `#tl` and `#br` anchor to opposite corners, so the bottom-right one also measures that a `bottom`
  or `right` offset is applied after the box has been sized rather than before.
- `#stretch` gives both `left` and `right` with an auto width, the one arrangement where an
  absolute box fills its containing block instead of shrinking.
- `#fit` gives only `left`, so it shrinks to fit its content, the same rule a float follows.
- `#body` is the only in-flow content, so the frame's height comes from it alone. None of the four
  absolute boxes contributes to it, though two are taller than it — which `#after` records by
  sitting where the frame ends rather than where its contents do.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

