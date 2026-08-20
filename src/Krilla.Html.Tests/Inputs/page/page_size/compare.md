# page/page_size

A box that exactly fills one Letter page at 96 DPI: 816 x 1056 CSS pixels, here as 1040px of
content inside an 8px border. If this paginates to two pages the page height is off by a rounding
step somewhere, and every multi-page scenario will be wrong in the same way.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

