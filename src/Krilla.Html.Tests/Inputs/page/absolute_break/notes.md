# page/absolute_break

An absolute box is painted from the root rather than where it was declared, and it is placed in
continuous coordinates by a second pass that runs after flow and knows nothing about pages. Which
page it lands on is therefore decided last, by the painter, from a position computed by neither of
the two stages that produced it.

`#pinned` is declared at the top of a frame that begins on page one and offset to y=1150, which is
past the 1056 boundary. It belongs on page two, 94px down, and appears on page one not at all. The
frame's own line of text is what keeps page one from being a single flat colour, which
`BaselineHealthTests` reads as a page that rendered nothing.

What to look at: the box on page one is the hoisted absolute being assigned to its declaring
parent's page. The box missing entirely is it being clipped against the first page's box after
being assigned correctly — the same distinction `PdfPainter` draws between the page box and
`pageEnd`.
