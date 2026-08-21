# position/fixed

`position: fixed` is implemented, and reported as a diagnostic because its geometry is right
while only its paged-media behaviour is not: CSS says a fixed box repeats on every page, and
this places it on the one page its position falls on. Nothing checked the first half of that
claim.

One page, deliberately, so the repetition question does not arise and what is left is purely where
the box lands.

- **`#corner`** has no positioned ancestor, so both readings of "containing block" give the page
  and this measures the offsets alone, resolved against the page's edges.
- **`#inner`** is inside a `position: relative` ancestor. A fixed box's containing block is the
  page regardless, which is the single property that distinguishes it from an absolute box —
  `AbsoluteLayout` walks to the nearest positioned ancestor for both, so this arrangement is
  expected to sit at the frame's padding box plus the offsets rather than at the page's. That
  difference is the point of including it: it is a defect with a measurement rather than a comment.

It found the defect on its first render: `#inner` landed at (160, 500) rather than (40, 300),
out by exactly the frame's own margins, because `AbsoluteLayout` walked to the nearest positioned
ancestor for a fixed box as it does for an absolute one. It now carries the initial containing
block alongside the accumulated one and hands a fixed box the former. Both boxes are exact and the
page is pixel-identical.

This is still the only scenario in the corpus that reports a diagnostic, and `DiagnosticTests`
lists it by name as expected to. The report is about the construct rather than about this
document: on one page there is nothing for the repetition to do, and the reporter cannot know the
page count from the cascade.

What to look at when it moves: `#corner` shifting is the offsets or the page's own box. `#inner`
back at (160, 500) is the initial containing block being lost again on the way down the tree.
