# flex/basic

# flex/basic

The flexible-length resolution: CSS Flexbox §9.7, which is the part of flexbox that is an
algorithm rather than a table of positions. Six rows, each isolating one thing the loop has to get
right.

- **`#grow`** is `flex: 1` against `flex: 2` beside a fixed 90px item. The two share the 510 the
  fixed one leaves, one to two, at 170 and 340. What makes it a real test is the BASIS: `flex: 1`
  expands to `1 1 0%`, so both start from zero and the ratio is over the whole 510. An
  implementation that reads the omitted basis as `auto` sizes them from their own words instead and
  gets two numbers that look plausible and are not related to the ratio at all — which is exactly
  what AngleSharp's expansion invites, since it hands back the omitted components as empty strings.
- **`#shrink`** is the scaled shrink factor. 300 of content in 200 of room, with `flex-shrink: 1`
  on a 200px item and `2` on a 100px one: the factors are scaled by each item's own base size, so
  1×200 and 2×100 carry equal weight and the two give up 50 each. Sharing the shortfall by the raw
  factors gives 33 and 67, which reads as reasonable and is wrong.
- **`#basis`** is three ways of naming a base size that none of them flexes away from — a
  percentage, a `width` reached through `flex-basis: auto`, and content.
- **`#none`** is `flex: none`, which AngleSharp drops from the cascade ENTIRELY: no longhand comes
  back at all, so it is recovered from the stylesheet's own text. Without that it is silently the
  initial values, which differ from `0 0 auto` in the shrink factor alone — invisible until the row
  overflows.
- **`#bounds`** is what makes §9.7 a loop rather than one division, and it found two defects. Three
  items at `flex: 1` each want 200 of the 600; one is capped at 140 and one floored at 200, and the
  60 the cap released has to go back to the others, which end at 230 apiece.

  The first defect was the FREEZE: the specification freezes the items whose own clamp moved them,
  and this froze the items whose size exceeded their base — which is every item that grew at all,
  so the first pass froze the lot and the released space went nowhere. The second was the remaining
  free space, which has to be measured against each unfrozen item's BASE size and was being
  measured against whatever the previous pass had clamped it to; that made the sixty pixels
  invisible a second time, and the row came out at 140, 200 and 33 with two thirds of the container
  empty.
- **`#halves`** is the sub-one factor rule: grow factors summing below one hand out only that
  fraction of the free space, so the row is deliberately left unfilled.

**Residual**: SSIM 0.9996 on FORTY pixels, all of them one column at x=352 — `#b3`'s right edge,
which lands at 352.5 because its max-content width is 82.5. A box edge at a fractional position,
the same residual `position/absolute` records, and not a geometry difference: all 23 boxes are
exact.

**Boxes**: 23 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 0.9996** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

