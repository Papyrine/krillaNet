# block/margin_collapse_parent

Collapse through a parent with no border or padding to stop it. The inner top margin escapes
through the outer top edge, so the outer box starts 40px down and does NOT include that margin in
its own height. The bottom margin escapes the same way.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

