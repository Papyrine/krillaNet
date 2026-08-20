# inline/text_align

Alignment moves runs within the line box without changing the paragraph geometry, so the boxes here
should match exactly while the pixels carry the whole signal. A scenario where the two metrics
deliberately disagree about what they can see.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

