# ua/list_markers

The marker styles `ua/lists` does not reach: the third nesting level, the counter styles other than
decimal, and the three ways an ordered list can be numbered by something other than its position.

Each list isolates one rule:

- The unordered lists cycle disc, circle, square with depth, which is a user-agent rule rather than
  anything the author asked for.
- `start` and `value` both move the counter rather than only the item carrying them, so the item
  after `value="20"` is twenty-one.
- `reversed` counts down from the number of items, which is the one case that has to know how many
  items a list has before it can number any of them.
- The alphabetic list crossing 26 measures the one piece of non-obvious arithmetic in the counter
  styles. The alphabet is bijective base 26 — there is no zero digit — so 26 is `z` and 27 is `aa`,
  where ordinary base 26 would give `a` followed by nothing and skip a value at every power.
- `list-style-type: none` is here to check that it draws nothing rather than falling back to a
  marker, since an unrecognised value deliberately does fall back.
- The padded item measures what the marker is positioned against — the item's border edge, not its
  content edge — so its padding moves the text and leaves the bullet behind.
- The larger list measures the size rule, which steps in whole pixels off the item's own ascent.

Item text is deliberately short. A marker is a handful of pixels, so a scenario carrying sentences
would report mostly text rasterisation and a marker regression could hide inside the number.

Markers are invisible to the box comparison — they generate no element box, and the browser reports
no rect for them — so only the pixel metric measures anything here. The box geometry being exact
says the lists laid out correctly, not that anything was drawn.

The render is not pixel-identical, and the residual is two named things rather than a mystery:

- Every circular marker differs by a few levels of grey on its antialiased edge, because a circle
  reaching the PDF as four cubics is not bit-identical to the curve Chrome emits for the same
  circle. Largest on the 32px list, where the bullet is ten pixels across; nowhere above 14 of 255.
  The square marker and every counter marker are pixel-identical.
- The word "Twenty" differs on one glyph. That is the sub-pixel glyph positioning difference the
  todo records as the largest residual in the engine, and it has nothing to do with markers — it is
  the same effect `text/kerning` exists to measure.

**Boxes**: 32 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0002 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

