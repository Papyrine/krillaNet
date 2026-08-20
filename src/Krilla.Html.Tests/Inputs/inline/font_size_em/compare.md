# inline/font_size_em

The em trap. A relative font-size resolves against the PARENT size, while every other em in the
same rule resolves against the size being computed. So the inner box is 16px with 8px of padding,
not 16px with 10px. A unitless line-height multiplies each element's own size.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

