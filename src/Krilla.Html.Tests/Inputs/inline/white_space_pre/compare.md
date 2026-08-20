# inline/white_space_pre

The two halves of white-space processing side by side: preserved above, collapsed below. The pair
matters because collapsing is what stops indented markup from indenting the page, and getting the
phase order wrong only shows up when a newline has spaces around it, which is what all indented
markup is.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

