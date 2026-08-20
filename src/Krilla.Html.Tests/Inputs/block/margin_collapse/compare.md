# block/margin_collapse

Adjacent siblings. The 30px below the first box meets the 50px above the second and collapses to
50, not 80. The first box's own top margin collapses out through body and html, so it starts at 30
rather than 0.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

