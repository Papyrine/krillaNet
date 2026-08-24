# page/break_before

# page/break_before

The first scenario in the corpus whose page COUNT is decided by a declaration rather than by how
much content there is. Three boxes of 48px each on a 1056px page: the document is 144px tall and
fits five times over, and Chrome still prints two pages because `#two` asked to start one.

That is the whole of what it isolates. `Paginator` used to run its loop while content remained
below the boundary — `top + pageHeight < documentHeight` — which is exactly the condition a forced
break in a short document fails. So the property could be read, resolved and looked up correctly
and still produce one page, with nothing in the box geometry to say so.

The break lands at `#two`'s top border edge, which is also where the box after it would have gone,
so nothing here can distinguish a break taken at the declaring box's top from one taken at the
previous box's bottom. `page/break_after` is where those two are pulled apart.

Both pages are pixel-identical to Chrome.

What to look at: the page count first — one page means the forced break never reached the loop
condition. Then whether page one ends after `#one`, and whether page two holds both `#two` and
`#three` rather than only `#two`.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |
| **Page 2** | **Page 2. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0002.png" width="480"> | <img src="result%23page_0002.verified.png" width="480"> |

