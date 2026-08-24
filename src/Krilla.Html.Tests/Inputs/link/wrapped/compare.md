# link/wrapped

An anchor spanning several lines. A PDF link is a rectangle, so one that wraps needs one annotation
per line fragment rather than a single box around the lot — a single box would make the blank space
at the end of each line clickable, and on a centred or short line would cover text that is not part
of the link at all.

Expect one annotation per line the anchor touches, each covering only its own fragment.

**Boxes**: 4 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 0.9999** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

