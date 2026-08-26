# table/caption_side

Pixel-identical to Chrome. Three tables, the first two differing in one declaration, so what is
measured is the property rather than the look of a caption.

A caption is a block laid out at the table's own width, above or below the grid, inside the table's
box. The measurement that matters is the gap: Chrome puts a bottom caption exactly as far under the
last row as a top one sits above the first, and that gap is the table's own edge spacing rather
than anything the caption carries. So the bottom caption goes after the grid's trailing edge
spacing, which the grid had already added — the alternative, adding a gap of its own, doubles it.

`#top` states the default explicitly. It is there so the two rows differ in exactly one
declaration, which is what makes a difference between them attributable.

`#declared` writes the property in the other place CSS allows: on the CAPTION rather than on the
table. That half reached nothing — the side was read off the table's style alone — and it is not a
spelling nobody uses, because `<caption align="bottom">` maps onto exactly that declaration. The
property is inherited now and read off the caption's own box, which is what makes both spellings
work for the same reason.

What to look at: the 2px band between the caption and the grid in each table. Four pixels in any of
them is the edge spacing being applied twice, and `#declared`'s caption back above its grid is the
property being read off the table again.
