# block/individual_transforms

`translate`, `rotate` and `scale` are not shorthands for `transform` and reach no longhand of it, so
a document written in the modern spelling moved nothing — and, until the audit that found them, was
told nothing either.

CSS Transforms 2 §3 composes them ahead of `transform` in a fixed order — translate, then rotate,
then scale, whatever order the declarations were written in — which makes them a PREFIX on the
function list rather than a second matrix. Everything downstream then applies to the composite
without knowing they exist, `transform-origin` included, which is what `#origin` pins.

- `#moved`, `#turned` and `#sized` are the three on their own.
- `#movedone` leaves its vertical component out, which is zero, and `#sizedpercent` writes its
  factor as a percentage — a scale factor rather than a fraction of anything, so it is `scale: 1.5`
  spelled differently.
- `#turnedz` names the z axis, the only one with a two-dimensional meaning. Naming it is the same as
  naming none; naming x or y, or giving three numbers, is three-dimensional and drops the whole
  composite the way `rotate3d()` inside `transform` does.
- `#composed` is the row the fixed order exists for. It is written scale-first and composed
  translate-first, so the 20px movement is NOT scaled; `#written` is the same three functions inside
  `transform`, which composes left to right, and comes out somewhere else. Two rows that would be
  identical under either reading if the order were the same, and are not.
- `#beside` puts one of each on a box, which is the arrangement that says the individual properties
  come first rather than being appended.
