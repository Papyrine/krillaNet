# block/background_color

A background paints the border box, and a child paints over its parent. Flat fills with hard edges,
so this scenario should reach near-identical pixels. If it does not, the units or the DPI are
misaligned and no text scenario's numbers can be trusted yet.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** _(no page)_ | **Page 1** |
|  | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

