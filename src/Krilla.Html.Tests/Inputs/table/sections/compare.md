# table/sections

Row groups, captions and vertical alignment.

- `#ordered` writes its sections in the order tfoot, thead, tbody and expects them rendered as
  thead, tbody, tfoot. That reordering is the whole point of the elements — it lets a long table's
  markup put the footer next to the data it summarises — and a table that renders sections in
  source order looks right until a document uses `tfoot` early.
- `#captioned` measures the caption's own box, which spans the table and sits above the grid with a
  border-spacing gap below it.
- `#captionwide` measures the rule that a table is never narrower than its caption's longest word.
  A caption of one long word with a single narrow cell is the case where the caption alone decides
  the table's width. It is the caption's MIN-content width that applies, not its maximum: a long
  caption wraps rather than stretching the table out.
- `#aligned` measures `vertical-align` in a row taller than three of its cells. The default is
  `middle` rather than the `baseline` the property's initial value suggests, because the user-agent
  stylesheet sets it on the table and the cells inherit — so a converter that honours only the
  initial value puts every short cell's text at the top of its row.

The render is not quite pixel-identical. Two words differ on a glyph each, which is the sub-pixel
glyph positioning difference the todo records as the engine's largest residual. A table shows it
more readily than most layouts because column widths are fractional by nature, so almost no cell
starts on a whole pixel.

**Boxes**: 33 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0002 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

