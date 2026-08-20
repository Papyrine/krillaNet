# ua/headings

The six heading defaults, with no stylesheet of their own. Each has a distinct font size and a
distinct margin, and the margins run the opposite way to the sizes: the smallest heading has the
largest one. AngleSharp ships the HTML 4.01 values, which differ from the modern ones on every
level below h1, so this scenario is the whole reason UserAgentStyles.Corrections exists.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

