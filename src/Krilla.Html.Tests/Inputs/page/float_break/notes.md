# page/float_break

A float is out of flow but not out of the page, and no scenario had one crossing a page boundary.
`#tall` runs 900..1200 with the boundary at 1056, so it has to be painted on both pages — its
upper 156px on the first and its lower 144px on the second — and the lines beside it have to keep
being shortened after the break.

The two halves of that are independent and can fail separately: the float's own box is painted by
`PdfPainter` per page, while the band the lines are laid against comes from `FloatContext` in
continuous coordinates, before pagination exists.

What to look at: the float's colour reaching the bottom edge of page one and resuming at the top
of page two, and the paragraph's first lines on page two still starting 120px in. Full-width lines
on page two are the band being lost at the break; a float painted on one page only is the box
being clipped to where it was declared.
