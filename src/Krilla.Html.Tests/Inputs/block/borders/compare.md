# block/borders

Four edges, four widths, four colours. Deliberately mismatched: with a uniform border a corner
mitre is invisible, and this renderer paints corners as overlapping rectangles rather than mitring
them. The corners are where that difference shows.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

