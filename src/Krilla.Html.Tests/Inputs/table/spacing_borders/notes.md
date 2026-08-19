The separated border model: what `border-spacing` separates, and what a border and a background
land on once it has.

- `#spaced` sets both axes independently. The gap goes outside the first and last column as well as
  between them, which is what makes a table with spacing wider than the sum of its columns.
- `#framed` puts a border on the table and on two diagonally opposite cells, so the cell borders are
  measured against the table's own rather than confused with it. The cell borders are inside the
  spacing, not shared with a neighbour — that is what separated means.
- `#painted` fills a row in one table and a cell in another. A row's box spans the whole grid rather
  than only the cells in it, so a row background reaches across the spacing while a cell background
  stops at the cell.
- `#padded` gives one cell asymmetric padding, which has to widen its column and raise its row
  without disturbing the other cells.

`border-collapse: collapse` is not implemented and no scenario here asks for it. It is a different
model rather than a variation on this one — collapsed borders are shared between neighbours, and
half of each sits outside the cell it was declared on.

The render is not quite pixel-identical, and the cause is worth naming because it is not a layout
difference: the box geometry is exact. A column width is fractional, so a cell's border lands on a
fractional pixel, and the two engines resolve that differently — Chrome snaps a box decoration to
whole device pixels before painting it, while this draws the edge where the geometry puts it and
lets it antialias. The difference is a fraction of one pixel along each border edge, never more
than 40 levels of grey out of 255. It is the same effect `image/inline_flow` records for an image
edge.
