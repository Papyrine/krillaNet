# block/transform

# block/transform

Pixel-identical to Chrome, and — the part worth more — geometry-exact as well, which for this
property is a stronger statement than it usually is.

A browser's `getBoundingClientRect()` returns the VISUAL rectangle, so a rotated 60×40 tile comes
back as the 71.96×64.6 box that encloses it. The corpus compares against that rect, so a transformed
element had two possible treatments: exempt it, or compute the same visual box on this side. The
second was taken, so `BoxDump` reports transformed rectangles and the geometry comparison becomes a
real check of the matrix arithmetic — composition order, the origin conjugation and each function's
own matrix are all pinned by numbers rather than by pixels alone.

That is also why `BoxDump.Collect` walks recursively rather than over `Descendants()`. A transform
applies to a box AND everything under it, so a transformed box inside another carries both, and a
flat walk has nowhere to keep the matrix that says so. The painter gets the same composition free
by nesting its pushes.

Seven rows:

- **`#translate`**, **`#scale`**, **`#rotate`** and **`#skew`** are one function each, so a wrong
  matrix for any of them is localised rather than mixed into a product.
- **`#composed`** pins the ORDER. `translate(30px, 0) rotate(15deg)` composes left to right, which
  for column vectors means the product in written order and so the rightmost function reaching a
  point first — the box is rotated about its origin and then moved, not moved and then rotated
  about where it started. The two differ by where the pivot ends up, and both look reasonable.
- **`#origin`** pins the pivot. The default is the centre of the border box, not its top-left, so
  naming a corner is a visible change rather than a no-op.
- **`#untransformed`** with **`#after`** pins that a transform changes PAINTING and not layout:
  the scaled tile reports 84×56 where its layout box is 60×40, and its sibling sits exactly where
  it would have without the transform. Nothing measured against a transformed box moves, which is
  the same bargain `position: relative` strikes.

A transform creates a stacking context, so a transformed box leaves its parent's paint phases and
goes down with the positioned content — which cost nothing to add here because `opacity` had
already built that machinery. The transform is pushed OUTSIDE the fade, so a box carrying both is
faded and then drawn through the transform rather than the other way round.

The three-dimensional functions are left unparsed rather than flattened. `rotate3d` has a
two-dimensional shadow that would put the box somewhere plausible and wrong, so the whole transform
is dropped and `UnsupportedCss` says so.

What to look at: `#composed` and `#origin`, the two rows whose answers are not forced by a single
function. A `#composed` that matches while `#origin` does not means the origin conjugation is
missing; both wrong together means the composition order is reversed.

**Boxes**: 17 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

