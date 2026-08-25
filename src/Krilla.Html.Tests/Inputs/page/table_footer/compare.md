# page/table_footer

# page/table_footer

A `tfoot` re-drawn at the foot of every page its table continues onto — the mirror of
`page/table_header`, and the half that was left undone when the header landed. A ledger long enough
to need three sheets, with no header group at all, so nothing here is measuring the header path by
accident.

Three sheets, and each says something different:

- **Page one** carries the repeated footer, and where it sits is the finding. Chromium draws it
  IMMEDIATELY BELOW the last row that fitted, not flush with the paper: the last row ends 941px
  down, the footer occupies the 60px after that, and the remaining 55px of the sheet is blank. So
  the band is reserved out of the page's height — which is what moves the break earlier — and then
  drawn at the position the break landed on rather than at the page's bottom edge.
- **Page two** holds the end of the table, where the real footer is drawn in place by ordinary
  painting. Nothing repeats here, and a converter that repeated unconditionally would draw the
  footer twice on one sheet.
- **Page three** is the closing paragraph alone. The rule is the table's own extent rather than the
  page count: a page beginning past the last row carries nothing, and a converter that repeated for
  as long as the document lasted would put a totals row on a sheet with no table on it.

The reservation is circular in appearance and not in fact. Whether a footer repeats depends on
where the page ends, and where the page ends depends on what is reserved — but reserving can only
move the end EARLIER, and a table that continued past the later end continues past the earlier one
too. One refinement settles it.

## What page two found

The first render put every pixel on page two half a pixel out, and the cause was not the footer.

A page begins at an unbreakable unit's top edge, and under `border-collapse` a row's top edge is
half a rule below a whole pixel — so this page's content started at document y 940.5. Every
rectangle this engine fills is snapped, but in LAYOUT units, and a layout-unit snap only lands on a
device pixel when the page's own offset is a whole number of them. It was not, so every edge on the
sheet was drawn antialiased down both sides: SSIM 0.9286 with the geometry exact, which is the
signature of a rasterisation-level problem rather than a layout one.

`PdfPainter` snaps the page's own shift now. It was invisible before this scenario because a
repeated HEADER's band runs from the table's top edge to the group's bottom, which carries the same
half pixel and cancels it — so `page/table_header` was already integral and had nothing to say.

**Residual**: page three is identical, and pages one and two differ only on glyph edges at 24 and
30px — the same sub-pixel glyph positioning `page/table_header` records. Every rule, fill and edge
agrees.

What to look at: whether the footer appears on page one at all, and whether it sits directly under
the last row rather than at the foot of the sheet. A footer flush with the paper is the reservation
being drawn in the wrong place; a footer missing from page one is the reservation not happening; a
footer on page three is the table's extent not being consulted.

**Boxes**: 99 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0044 · SSIM 0.9992** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |
| **Page 2** | **Page 2. AE 0.0114 · SSIM 0.9984** |
| <img src="reference_0002.png" width="480"> | <img src="result%23page_0002.verified.png" width="480"> |
| **Page 3** | **Page 3. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0003.png" width="480"> | <img src="result%23page_0003.verified.png" width="480"> |

