# ua/lists

List indentation, which AngleSharp omits entirely — before the corrections an unstyled list had no
padding-left at all and sat flush against the margin. Also covers the nested case, where the inner
list drops its vertical margins so a multi-level outline reads as one block.

List markers are not drawn, so the bullets and numbers a browser shows are absent from the render.
That is a real gap and it shows in the pixel metric; the box geometry is unaffected, because a
marker sits outside the principal box.

**Boxes**: 12 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0001 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

