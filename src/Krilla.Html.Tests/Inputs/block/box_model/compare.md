# block/box_model

The box model in one box. The border box is 200+40+10 wide and 100+20+10 tall; the margin moves it
without growing it. Everything downstream assumes this is right, so it is the first thing to check
when a whole category goes wrong at once.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

