# inline/wrapping

Greedy line breaking at spaces. Where each break lands is decided by accumulated advances, so a
measurement error of a fraction of a pixel per glyph eventually moves a word to the next line and
changes the paragraph height. The box comparison catches that far more sharply than the pixel one.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

