# block/margin_collapse_blocked

The other half of margin_collapse_parent. A single pixel of top padding and a bottom border are
enough to stop the collapse in both directions, so both inner margins stay inside and the outer box
grows by 80px. The pair is what proves the rule rather than a coincidence.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

