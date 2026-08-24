# table/empty_cells

# table/empty_cells

Pixel-identical to Chrome, and the geometry is what confirms the property: it must not move.

`empty-cells: hide` suppresses an empty cell's border and background and nothing else. The cell
keeps its place in the grid, the rows do not close up, and every cell in `#hidden` reports the same
54x28 as its counterpart in `#shown`. That is the whole difference from `display: none`, and it is
why a scenario measuring this needs both tables laid out identically.

`#whitespace` is the row worth having. A cell containing a single space counts as empty, which
follows from something the engine already does rather than from a rule about tables: collapsible
white space generates no inline content, so a cell written with a space in it produces exactly what
a cell written with nothing produces. Measured, and what Chrome does.

The suppression is tested on the LAID-OUT box — no children, no lines, no floats, no positioned
descendants and no image — rather than on the element, because the question is whether anything
was generated rather than whether anything was written. An item that was written and then removed
by `display: none` leaves an empty cell, and a browser hides that one too.

The property is inherited and read on the cell, which is how a declaration on the table reaches
its cells: the same route `vertical-align: middle` takes, and for the same reason.

What to look at: the borders in `#hidden`. Five of the six cells should be blank paper, and the
grid should be laid out identically to `#shown` above it.

**Boxes**: 28 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

