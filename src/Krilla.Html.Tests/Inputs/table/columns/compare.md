# table/columns

# table/columns

`<col>` and `<colgroup>` widths, which were reported as reaching nothing — so a table sized entirely
through its column definitions, which is how reporting tools and mail merges write one, got automatic
widths instead. Geometry is exact on all sixty-three boxes and the page reads SSIM 0.9997.

A column definition generates no box, so its width has nowhere of its own to live: it rides on the
table as a positional list, one entry per column, which is what lets the sizing algorithm read the
nth column's declared width without knowing anything about the elements that declared it. `span`
REPEATS the width rather than sharing it out, and a `<colgroup>` holding `<col>` children contributes
nothing of its own.

**The first defect was invisible in one of the two forms.** A `<colgroup span="2">` with no children
worked immediately and every `<col>` in the document was ignored, because the branch that recognises
a column definition returns before the ordinary child walk — so the `<col>` elements were never
visited. `#grouped` and `#lengths` differ in nothing but that, which is what made it findable.

**The second was a data race.** The pending widths lived in a static field on the box builder, which
is static and reached by concurrent conversions: `BoxFidelityTests` walks the corpus in a loop and
`CorpusTests` runs it in parallel, and the two reported different geometry for the same scenario from
the same reference. They now live on `DocumentContext`, beside the CSS counters, for the same reason.

Then the scenario found two rules in the column algorithm that no cell-based scenario could reach. A
table whose columns are ALL pinned needs every one of them declared, and a cell declaring a width in
every column of every row is rare enough that none had it:

- **A surplus is shared in proportion to the widths themselves.** Three columns wanting 220, 60 and
  60 with 132 left over come out 305.4, 83.3 and 83.3.
- **A shortfall is shared in proportion to what each column can GIVE UP** — its width less its
  min-content width. Three wanting 220, 220 and 60 with 28 too many come out 207.5, 207.5 and 57.0,
  where shrinking by width gives 207.7, 207.7 and 56.6. Four tenths of a pixel, and the whole of what
  separates the two readings: the wide columns give up nearly all of it either way and only the narrow
  one moves.
- **And a declared column width does not raise the floor under a table that declares its own width.**
  `#spanned` keeps its 480px and shrinks its columns; counting the declared widths grew the table to
  500 and left nothing to distribute. It still counts for a table with no width of its own, which is
  what `table/spans` caught when both cases were given the same answer.
- **The residue goes to the LAST column, on Chrome's 1/64 pixel grid.** Two identical columns come
  out 83.28 and 83.31, three hundredths apart, because the second is handed what the division left
  over. Sharing it evenly leaves them identical, which is small enough to look like noise and is the
  difference between exact and nearly exact.

`<col>` also had to become measurable. A browser reports a rectangle for one — its column's extent by
the height of the row area — so without that the comparison counted all twenty-one column definitions
in this scenario as elements the engine did not produce, and
`BaselineHealthTests.EveryElementIsMeasured` said so.

What to look at: the column widths in `#lengths` — 220, 60, 192 — and in `#spanned`, where none of
the three is the width it asked for.

**Boxes**: 63 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

