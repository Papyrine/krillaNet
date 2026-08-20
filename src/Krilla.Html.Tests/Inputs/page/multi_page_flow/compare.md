# page/multi_page_flow

Enough text to run past one page. Both engines break between lines rather than through them, which
is why the reference is printed rather than screenshotted and sliced: a sliced screenshot would cut
a line in half at every boundary and report a difference that came from how the reference was made
rather than from anything either engine did.

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

