# link/external

Two external links. Neither the pixel nor the box comparison can see a link annotation — it paints
nothing and is not an element box — so the annotations are read back out of the PDF and recorded in
the snapshot instead. Those two metrics staying at zero is the separate check that adding links
disturbed no layout.

The rectangle covers the text's em box rather than the whole line, so a generous line-height does
not make blank space clickable.

**Boxes**: 4 matched, worst offset 0.00px, worst size 0.00px.

Not rendered: `html > body:nth-child(2) > p:nth-child(1) > a:nth-child(1)`, `html > body:nth-child(2) > p:nth-child(2) > a:nth-child(1)`

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0003 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

