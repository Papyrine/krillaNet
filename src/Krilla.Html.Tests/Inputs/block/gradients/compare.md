# block/gradients

# block/gradients

`linear-gradient()` and `radial-gradient()`, which were parsed by the cascade and then dropped —
so a box designed as a ramp came out as bare paper.

Nine rows, and every number in them was checked arithmetically rather than by eye. A colour sampled
at a known pixel is a linear function of the gradient's geometry, so one sample pins the gradient
line exactly: the 45° row's left edge reads 177 where the line running through the box centre at
`|W·sin A| + |H·cos A|` long predicts 177.2, and the ellipse's left edge reads 102 where a curve
through the farthest corner predicts 101.5. That is why the rows are flat colour ramps with no text
— the measurement is the pixel values, not the look.

What the rows pin:

- **`#to-right`** and **`#default`** together say the default direction is `to bottom`, not
  `to right`, and that interpolation is plain linear sRGB — the midpoint of `#c04040` and `#4060a0`
  comes back as exactly `(128, 80, 112)`.
- **`#angle`** puts the start colour exactly on the bottom-left corner and the end exactly on the
  top-right, which is the signature of the gradient-line length above and is what makes a 45°
  gradient in a box that is not square different from one that merely runs corner to corner.
- **`#stops`** places a stop off centre; **`#hard`** puts two at the same position.
- **`#radial`** and **`#ellipse`** say the default shape is an ELLIPSE, sized `farthest-corner` —
  which works out at exactly √2 times the half-width and half-height, so the left edge of a 200×60
  box sits at 0.7071 along the ramp rather than at 1.
- **`#padded`** is where the gradient's box stops being the box it is painted into.
- **`#over-colour`** is a translucent ramp over a background COLOUR, which says the two are layers
  of one background rather than alternatives.

Three things came out of the measurements that a reading would not have given.

**A browser TILES the gradient.** `background-repeat` defaults to `repeat`, and the gradient's box
is the PADDING box while the paint reaches the BORDER box — so the strip under a border carries the
end of the previous tile rather than the edge colour. `#padded` is bluish at its left border and
reddish at its right, which is the opposite of what padding the ramp gives. An axis-aligned ramp is
uniform perpendicular to its own axis, so repeating it along that axis IS the two-dimensional
tiling, and that is what the implementation does — but only where the paint actually reaches past
the gradient's box, since repeating inside it wraps the last column back to the start colour.

**A hard stop cannot be expressed directly.** A PDF shading's stitching function needs strictly
increasing bounds, so two stops at the same offset leave a zero-width step that is dropped, and the
edge becomes a ramp across the whole box. Nudging the second onto the next representable float
keeps the edge within a sub-pixel. `#hard` is pixel-identical with the nudge and unrecognisable
without it.

**A corner keyword never arrives.** AngleSharp rewrites `to top right` as `45deg` before this
engine sees it, which is right only for a square box — the real angle is perpendicular to the
diagonal joining the other two corners, so in a wide, short box `to top right` is nearly `to top`.
It is indistinguishable from an angle the author wrote, so it cannot even be reported. The row that
measured it was removed for that reason and the limitation is recorded in the readme;
`GradientPaint.Resolve` still holds the correct resolution, against the day the value survives.

**Residual**: SSIM 0.9999, and every differing pixel is within one or two of 255. That is
quantisation along the ramp rather than a difference in geometry — `#stops` and `#hard` are exactly
identical, and no pixel anywhere differs by more than two. The `AE` figure is high for the same
reason: it counts any nonzero difference, and a smooth ramp has thousands of pixels a shade apart.

What to look at: the corners of `#angle`, which are where the gradient line's length shows, and the
border strips of `#padded`, which are where the tiling does.

**Boxes**: 11 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0500 · SSIM 0.9999** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

