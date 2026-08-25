# block/inset_shadow

`box-shadow` with `inset`, which shades the inside of the box rather than casting outside it. Every
layer carrying the keyword was dropped and reported.

It is the one thing in the shadow grammar that needed no Gaussian. A blurred shadow cannot be drawn
at all here — a PDF content stream cannot express a Gaussian for an arbitrary shape, and a blurred
shadow drawn sharp is a hard dark copy where a soft halo belongs — but an inset one with no blur is
a SUBTRACTION: the padding box, less the same rectangle moved by the offset. A positive offset
therefore leaves a band along the top and the left, which is what makes a box look pressed in.

## The rows

- **`#both`** offsets on both axes: a band across the top and down the left. Measured, and the
  edge it starts at is the finding — the shadow begins at the inside of the box's 4px border rather
  than at the box's own edge, so it is the PADDING box being subtracted from and not the border box.
- **`#negative`** is the same with negative offsets, which puts the bands on the bottom and the
  right. It exists because a subtraction implemented as "fill the top and left bands" passes
  `#both` and gets this backwards.
- **`#one-axis`** offsets vertically alone, leaving one band and no corner.
- **`#padded`** has padding, and shows that the padding box is the padding box: the shadow reaches
  across the padding rather than starting at the content edge.
- **`#layers`** stacks two inset shadows. They paint farthest-first like any other shadow list, so
  the one written FIRST ends up on top — which two overlapping bands are what makes visible.
- **`#mixed`** carries an inset layer and an outer one on the same box, which is what pins where
  each goes in the paint order: the outer one is behind the background and the inset one over it,
  so a box with both is shaded inside and casts outside at once.

Pixel-identical to Chrome, and exact on all 8 boxes — which is expected rather than lucky, since
nothing here moves a box.

The clip is doing real work rather than tidying up: the offset copy of the padding box hangs OUT of
it on the far side, and under the non-zero winding rule the region outside the outer contour but
inside the inner one comes out filled. Without the clip the shadow leaks past the box's own edge in
exactly the direction it was offset.

What to look at: which EDGES carry a band, and whether the band starts at the border's inner edge.
Bands on the wrong sides is the offset's sign; a band starting at the box's outer edge is the border
box being used where the padding box belongs.
