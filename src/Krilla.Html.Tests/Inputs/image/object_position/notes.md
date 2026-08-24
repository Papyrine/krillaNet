# image/object_position

Pixel-identical to Chrome. The last of the four properties named in the diagnostic audit as read by
nothing, and the smallest: it was silently the centre for every document that asked for anything
else.

`object-position` follows exactly the rule `background-position` follows, which is worth stating
because it is not the rule the syntax suggests. A percentage does not offset by a fraction of the
box: it aligns that fraction of the CONTENT with the same fraction of the box, so `25%` of the 96px
left over is 24px rather than 40. `left`/`top` are `0%`, `right`/`bottom` are `100%`, and the
initial `50% 50%` is the centring that was already there.

Every box is the same 160x60 and the image the same 64x32, so the geometry comparison confirms for
free what `object-fit` established: the property changes what is drawn inside the box and never the
box. `object-fit: none` on the first five rows keeps the content at its intrinsic size, which is
what makes the offsets readable as offsets.

`#covered` is the row that needs the property to apply AFTER the fit rather than before. Under
`cover` the content is scaled to 160x80 in a 60px box, so the vertical slack is negative and
`bottom` resolves to -20 — choosing which band of the image survives the clip. Applying the position
to the unscaled content instead would put it nowhere near.

What to look at: the left edge of `#proportional` at 24px, and `#lengths` at 20px. Equal values
there mean percentages are being treated as fractions of the box.
