# image/intrinsic

An image with no width or height takes its intrinsic size: 64x32 CSS pixels, one per image pixel.
The wrapper exists so the image's own box is visible against a background rather than against the
page.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

