# table/caption_side

Pixel-identical to Chrome. Two tables differing in one declaration, so what is measured is the
property rather than the look of a caption.

A caption is a block laid out at the table's own width, above or below the grid, inside the table's
box. The measurement that matters is the gap: Chrome puts a bottom caption exactly as far under the
last row as a top one sits above the first, and that gap is the table's own edge spacing rather
than anything the caption carries. So the bottom caption goes after the grid's trailing edge
spacing, which the grid had already added — the alternative, adding a gap of its own, doubles it.

`#top` states the default explicitly. It is there so the two rows differ in exactly one
declaration, which is what makes a difference between them attributable.

What to look at: the 2px band between the caption and the grid in each table. Four pixels in either
one is the edge spacing being applied twice.
