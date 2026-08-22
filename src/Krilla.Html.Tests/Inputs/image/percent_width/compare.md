# image/percent_width

# image/percent_width

A percentage width on a replaced element, with a surround thick enough to tell the two mistakes
apart.

`#plain` is 330 wide: the 50% resolves against the container's 600 and gives 300px of picture, then
the padding and border are added. The tempting reading — take the element's own surround off the
container first, then halve what is left — gives 315, and it is wrong in the direction that makes a
padded image narrower than an unpadded one asking for the same share.

`#inside` is `border-box`, so the 300 is the whole box and 270px of it is picture.

The heights are the check that the ratio is applied to the CONTENT box in both cases. The swatch is
64x32, so `#plain` is 150px of picture inside 180px of box and `#inside` is 135 inside 165 — a
box-sizing bug that only touched the width would leave both heights at 150.

`#ratio` declares a HEIGHT instead, under `border-box` and with padding that is not uniform — 10px
top and bottom against 30px left and right. Its 120px box holds 90px of picture, which the ratio
turns into 180px of width and 250px of box. It is here because the vertical surround has to be its
own quantity: deflating the declared height by the horizontal pair instead gives 50px of picture,
100px of width and a 170px box, and the error arrives on the axis nobody declared. That is exactly
the bug this scenario caught while it was being written.

**Boxes**: 6 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

