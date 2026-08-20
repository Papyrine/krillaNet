# image/inline_flow

An image is an atomic inline: it flows on the line like a word, breaks before or after but never
inside, and sits its bottom edge on the baseline. Because it is 32px tall in a 24px line, it pushes
the line's top upward rather than growing it downward, which is what baseline alignment means for a
replaced element.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

