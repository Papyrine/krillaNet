# ua/paragraphs

Paragraph spacing with no stylesheet. The default margin is 1em top and bottom, and adjacent
paragraphs collapse to one gap rather than two. AngleSharp uses the HTML 4.01 value of 1.12em, so
before the corrections every gap in an unstyled document was 12% too large.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

