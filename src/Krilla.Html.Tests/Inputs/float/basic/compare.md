# float/basic

What a float does to the text beside it, and what it deliberately does not do to the boxes.

Three things are measured here, and the third is the one that catches an implementation out:

- `#left` and `#right` shorten the line boxes of the paragraph beside them. The paragraph's own
  border box is unchanged and still spans the full 400px — a fact readily missed, because the text
  inside it moves and the box does not.
- `#tall` is taller than the single line beside it, so it hangs out of the bottom of `#wrap3`. A
  block does not grow to contain a float, and a document relying on that will overlap whatever
  follows. That is correct rather than a defect, and `#wrap3` records how far it overflows.
- `#block` sits beside the same float and is NOT moved or narrowed. CSS shortens line boxes, not
  block boxes, so an ordinary block overlaps the float. An implementation that narrowed the
  block instead would look plausible on this page and be wrong everywhere.

The backgrounds are what makes the difference between the second and third points visible in the
pixels rather than only in the geometry.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.00.verified.png" width="480"> |
| **Page 2** _(no page)_ | **Page 2** |
|  | <img src="result%23page_0001.01.verified.png" width="480"> |

