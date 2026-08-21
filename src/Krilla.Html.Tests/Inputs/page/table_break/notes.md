# page/table_break

Every multi-page scenario in the corpus was plain paragraphs, so pagination had only ever been
measured against the one construct it was written for. A table is the first case where the break
candidates and the boxes disagree: `Paginator` breaks before a LINE that would straddle the
boundary, and a browser's printer moves a whole ROW.

Six 36px rows starting at y=950, so the table runs 950..1166 and the boundary at 1056 falls inside
the third row rather than between two of them. A break taken at the line inside that row splits
the row's background and border across the two pages; a break taken at the row moves the whole
thing overleaf.

It found two defects, and they compounded. The break went to the top of the LINE inside row
three rather than to the row's own top edge, six pixels lower, so everything on page two sat six
pixels high — 0.9659, the lowest SSIM the corpus has recorded. And the row it left behind still
painted its cell backgrounds down to the paper, stranding a sliver of the moved row at the foot of
page one.

`Paginator.Unbreakable` now treats a table row as one unit, the way a line is one everywhere else,
and `PdfPainter.PaintBox` culls a box whose top edge is at or after the break — which is a
different test from clipping at the break, and deliberately so: a box the break falls INSIDE is
fragmented, and a browser fills the rest of the page with that fragment. `page/multi_page_flow` is
the scenario that says so, and clipping cost it 1.6% of its pixels before the distinction was
drawn.

The geometry stays at zero throughout, because it is measured in continuous coordinates where
there is no break at all — which makes this a difference only the pixels could ever report.

What to look at: the vertical offset of page two's first row, and any part of it appearing at the
foot of page one. Six pixels of either is the line-based break returning.
