# position/z_index

`z-index`, which decides the order overlapping positioned boxes paint in.

Nothing here moves a box, so the geometry comparison confirms the property by staying at zero and
the whole measurement is in the pixels. That is the same bargain `block/opacity` strikes, and for
the same reason: paint order is invisible until two boxes overlap.

- `#order` is the property itself. `#below` is written second, so document order would put it on
  top; the levels put `#above` there instead. The engine painted every positioned box in tree
  order before this, so this row is what the whole scenario was added for.
- `#tie` shares a level between two boxes, which sends them back to tree order. It renders the same
  with or without `z-index` implemented, and exists to pin the sort as a STABLE one — an unstable
  sort is correct on every arrangement where no two boxes share a level, which is most of them.
- `#behind` is the property's most quoted surprise: a negative box goes under the in-flow content
  and over the background behind it. It is what made the painter lift a box's own background out of
  the layer walk, since Appendix E paints the negative contexts BETWEEN the two and they were one
  walk before. `#under` is deliberately wider than `#cover`, so a red surround says it was painted
  at all — dropping it entirely would leave the same green rectangle otherwise.
- `#confined` is the rule that a level is measured against siblings rather than against the page.
  `#child` asks for 100 and still sits under `#sibling`'s 2, because `#parent` took a context at 1
  and nothing inside it can climb out. Comparing levels globally is the plausible reading, and puts
  `#child` on top.
- `#zero` is why `z-index: 0` is a real declaration. It establishes a context where `auto` does not,
  so `#raised` is held inside `#context` and `#over` covers it. Treated as `auto`, `#raised` would
  flatten onto the page at 5 and cover everything in the row — which is the difference between
  `ZIndex` being null and being zero, and the only place the corpus can see it.

The two rows using `#c8c8e0` give their context box a background of its own, so the confinement
shows as a coloured frame the child sits inside rather than as a bare pair of overlapping tiles.

**Boxes**: 19 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

