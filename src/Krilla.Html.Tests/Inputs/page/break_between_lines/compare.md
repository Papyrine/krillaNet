# page/break_between_lines

A break landing inside a paragraph. The spacer leaves 76px of the first page, which fits two lines
of 32 but not three, so the third line must move to page two whole. A renderer that slices at the
page height instead would cut it in half.

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

