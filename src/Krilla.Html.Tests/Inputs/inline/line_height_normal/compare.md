# inline/line_height_normal

Probes what `normal` resolves to, which no stylesheet states: it comes from the font ascent,
descent and line gap, and from whether OS/2 asks for its typographic metrics to win over hhea.
Deliberately isolated, because every other text scenario sets line-height explicitly so that this
one question cannot contaminate them.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

