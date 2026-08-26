# inline/border_radius

`border-radius` on an inline element painted square, and said so. The rounding itself was never the
difficulty — `RoundedBox` has drawn a block's corners since `block/border_radius` — the difficulty
was that an inline element's background is painted per RUN, and a corner belongs to the FRAGMENT
those runs make up.

`#three` is the row that makes the difference visible: a span holding a `<b>` is three runs, and
rounding each of them would notch the fill at every element boundary inside the phrase. So the
grouping is the whole of what the property needed, and `InlineFragments` is it — one rectangle per
element per line, spanning from the element's opening edge, or its first run where it has no
surround, to its closing edge.

- `#one` is a background alone, the common case: a badge.
- `#two` adds a uniform border, drawn as a ring — the inner corner is the outer radius less the edge
  running along it, which is what makes a thick rounded border read as a ring rather than a tube.
  The same construction a block's uniform border already takes.
- `#four` wraps. A break is not an edge of the element, so the fragment on the first line keeps its
  two LEFT corners and the one on the second its two right; neither rounds where the line ended, and
  neither draws a side border there. That is the same rule the square path already followed for the
  side borders, extended to the corners.
- `#five` measures the two shapes a radius can take: the slash form's ellipse quadrant, and an
  over-large radius, where CSS Backgrounds 3 §5.5 scales every radius on the box by one factor —
  giving a pill rather than a rectangle with two circular ends.
- `#six` is a gradient under a rounded fill. The ramp's box is the element's whole advance laid end
  to end, which the rounding does not change: the fragment shows the same slice it showed before.
- `#seven` is the case with no single ring to draw. Its four edges disagree, so each is painted
  inside the MITRE SECTOR its two diagonals bound, cut to the same ring `#two` is drawn as.

  It read as a corner problem and was a COVERAGE one. The edges used to be axis-aligned bands along
  each side, and a band cannot reach a rounded inner corner however it is clipped: part of the ring
  at a corner lies past the inner rectangle's corner and still short of the inner ARC — (19, 214)
  here, at a 10px radius over a 4px border on a box cornered at (14, 208). A clip only takes area
  away, so adding the inner outline to the clip, which is what "round the inner corner" sounds like
  it needs, changed not one pixel. Sectors reach it.

  The diagonal they split on is also where a browser divides a corner between two colours, so this
  settled a second difference nobody had attributed: the bands overlapped at each corner and the
  last one drawn took the whole of it, putting `border-right-color` on both right-hand corners.
  Eighty pixels of the frame were wrong by up to 210 of 255, and twenty more by up to 71.

  What is left is eight pixels at up to 27 of 255, on the mitre diagonals: two antialiased sectors
  meeting there do not composite to full coverage, which is the residual `block/bevelled_borders`
  records for the same reason and the reason a uniform border is still drawn as one ring instead.
  The scenario reads SSIM 1.0000 and reports nothing.

Painting a fragment as a unit had to leave the paint ORDER alone, and does: a fragment is drawn at
the first run inside it, which is exactly where the per-run fill it replaces would have happened. So
a rounded element sits in the same place among its neighbours' backgrounds as an unrounded one, and
the pre-pass is built at all only for a box whose inline content is rounded — every other document
keeps the painting path it had, which is what the rest of the corpus staying identical says.
