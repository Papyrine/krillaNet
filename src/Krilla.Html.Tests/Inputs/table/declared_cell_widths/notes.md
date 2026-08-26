# table/declared_cell_widths

Pixel-identical to Chrome and exact on all 28 boxes.

A width declared on a CELL is a preference, not a floor. It pins the column when there is room and
is squeezed when there is not, and the column still may never go below what its content needs. Both
halves were wrong: the declaration reached the column's MINIMUM, which made the table itself grow to
hold it, and the distribution handed the pinned column its declared width whatever was left.

Found while applying `<td width>` as a presentational hint, which is where a document is most likely
to declare one — a reporting tool sizing a table by hand asks for widths that do not add up far more
often than a stylesheet does.

- `#capped` — the case both readings agree on, and the control: the declared 120px fits inside the
  table's 300px, so the column keeps it and the other takes the rest.
- `#roomy` — the same table with room to spare. The surplus goes entirely to the column that
  declared nothing, which is the rule for a table with some columns pinned and some not; a
  proportional share would widen the declared column too.
- `#shrunk` — no declared table width, where the declaration DOES raise the floor. A table with no
  width of its own is at least as wide as its columns asked to be, which is the distinction
  `ColumnSizes.MinTotal` and `ContentMinTotal` exist to keep.
- `#over` — the discriminating row. A cell asking for 700px in a table declaring 300px comes out at
  232.41, with the column beside it at its own min-content of 67.59 and the table exactly 300 wide.
  Before this the table was 783.59 and overflowed the page.

## What to look at when it moves

A table wider than it declared is the declaration back in the minimum. A cell at its full declared
width with the table clipped around it is the shortfall no longer coming out of the pinned columns.
And `#shrunk` narrower than 203.59 is the opposite mistake — the floor removed from the case that
needs it, which is what `table/spans` caught the last time this was touched.
