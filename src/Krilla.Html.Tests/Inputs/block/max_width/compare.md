# block/max_width

max-width with auto margins, the commonest centring idiom there is. It only works because CSS
re-runs the width algorithm once max-width has clamped an auto width, handing the leftover space
back to the margins. A naive clamp leaves the box at the left edge.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

