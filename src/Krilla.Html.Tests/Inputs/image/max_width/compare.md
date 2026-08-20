# image/max_width

max-width clamping with the height left auto. The declared 600px width is clamped to the 150px
container, and the height must be rescaled by the same factor to 75px. Skipping that rescale is how
images end up distorted inside responsive containers, so this scenario exists to catch it.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

