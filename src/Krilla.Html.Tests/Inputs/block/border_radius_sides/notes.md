# block/border_radius_sides

`border-radius` on a box whose border is **not** one uniform ring. The radius was honoured on the
fill underneath and lost on the frame over it, so a callout with `border-left` and a radius came out
with a rounded background inside square corners — and the diagnostic reported it rather than the
painter drawing it.

## How it is drawn

Not by building the curve into each side's outline. That would mean splitting an arc at the corner
and solving for where two edges of different widths hand over, per corner, per band.

Instead the trapezium each mitred edge already fills becomes a **clip**, and the full rounded ring
is drawn through it in that side's colour. Two things fall out for free:

- The diagonal bounding the trapezium — outer corner to inner corner — is where a browser
  transitions between two adjacent colours, so the split is right without computing it. `#widths`
  is the row that shows it is not 45° everywhere: with edges of 3, 12, 6 and 9 pixels, each corner
  hands over on its own angle.
- The arc is drawn by the same `RoundedBox` a uniform border already used, so there is one
  implementation of the curve rather than two that could disagree.

A band is the same construction between two nested rounded rectangles, which is why `#groove` — two
bands per side rather than one — needed nothing extra.

Square borders keep the polygon path untouched. That is what leaves every existing scenario
identical: the clip is reached only when a radius is actually asked for.

## The rows

| | |
|---|---|
| `#one` | One bordered side, the case the diagnostic used to report |
| `#two` | Two colours meeting on a corner diagonal |
| `#four` | Four colours, so every corner is a transition |
| `#widths` | Edges of 3/12/6/9px, where no corner splits at 45° |
| `#thick` | Radius under the border width, so the inner corner comes to a point |
| `#groove` | Two bands per side, each a ring of its own |

## Residual

SSIM 0.9988, and every border-coloured differing pixel is a thin seam at a corner transition —
roughly a pixel wide, where Chromium carries one colour a fraction further around the arc than the
outer-to-inner diagonal puts it. It is the same mechanism `block/bevelled_borders` records: two
antialiased fills meeting on a diagonal do not composite to full coverage, and the corner is where
that shows. The rest of the difference is sub-pixel glyph antialiasing in the six lines of text.

This is the one place in the corpus where a rounded border is not pixel-exact, and it is why the
report narrowed to **patterned** edges rather than disappearing: a dashed, dotted or double edge is
stroked along its own centre line and deliberately runs past the corner, so there is no corner there
for a radius to curve at all.
