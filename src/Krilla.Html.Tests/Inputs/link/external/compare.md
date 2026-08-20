# link/external

Two external links. Neither the pixel nor the box comparison can see a link annotation — it paints
nothing and is not an element box — so the annotations are read back out of the PDF and recorded in
the snapshot instead. Those two metrics staying at zero is the separate check that adding links
disturbed no layout.

The rectangle covers the text's em box rather than the whole line, so a generous line-height does
not make blank space clickable.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

