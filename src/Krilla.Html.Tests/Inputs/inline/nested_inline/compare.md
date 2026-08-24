# inline/nested_inline

Runs of different faces on one line. A line takes its height from the tallest inline box on it and
its baseline from the deepest, so a mismatched face changes both the wrap points and the line
positions. Note the explicit b and i rules: the shared reset flattens the UA stylesheet, so these
elements carry no styling of their own until a scenario gives them some.

**Boxes**: 7 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

