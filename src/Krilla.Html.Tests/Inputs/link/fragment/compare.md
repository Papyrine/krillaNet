# link/fragment

An internal link, and a broken one. The filler pushes the target onto the second page, so resolving
the fragment has to happen after pagination — a fragment names an element while a PDF internal link
names a page and a point on it.

The second anchor points at an id no element carries. It produces no annotation at all, rather than
one aimed at page one: a link that silently goes somewhere wrong is worse than a link that is not
there. Expect exactly one annotation.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** | **Page 2** |
| <img src="reference_0002.png" width="480"> | <img src="result%23page_0001.01.verified.png" width="480"> |
| **Page 3** _(no page)_ | **Page 3** |
|  | <img src="result%23page_0002.00.verified.png" width="480"> |
| **Page 4** _(no page)_ | **Page 4** |
|  | <img src="result%23page_0002.01.verified.png" width="480"> |

