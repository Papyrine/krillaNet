# image/sized

The three sizing paths. Width alone gives 192x96 from the 2:1 ratio; height alone gives 192x96 the
other way; both given wins over the ratio and the image is deliberately distorted to 150x150. The
width and height content attributes are presentational hints, which AngleSharp does not surface as
declarations, so they are applied after the cascade.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

