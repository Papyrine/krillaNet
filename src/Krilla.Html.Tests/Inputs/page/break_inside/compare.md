# page/break_inside

# page/break_inside

A box that would otherwise be split, asking not to be. The card runs 950..1070 with the boundary at
1056 falling inside its fourth line, so a break taken between lines lands at 1034 and leaves three
lines on page one and one line overleaf. `page-break-inside: avoid` moves the whole card instead,
and Chrome puts the break at 950 — the card's top border edge.

This is `page/table_break` reached from the other direction. A table row is one unbreakable unit by
rule, and a box carrying `break-inside: avoid` is one by request, so `Paginator.Unbreakable` yields
the box's border box in place of the lines inside it for both. Which means the case where the box
does not fit was already answered: a card taller than the page overflows rather than descending
forever looking for room, by the same guard in `NextTop` that a too-tall row uses.

Nothing here needed the painter. `PdfPainter.PaintBox` already culls a box whose top edge is at or
after the break, so the moved card paints nothing on page one — the distinction `page/table_break`
drew between a box moved WHOLE and a box the break falls inside.

It found something incidental on the way. The card's text originally contained the word
`page-break-inside`, and Chrome broke the line INSIDE it — `page-` ending one line and
`break-inside` opening the next — where this engine treated a hyphenated word as one unbreakable
token. The text was reworded to take the hyphen out, because a scenario measuring two things
reports one number for both.

That gap is now closed, and `inline/hyphen_breaks` is where it is measured. The wording here stays
hyphen-free anyway: this scenario is about where a page breaks, and a word that also decides where
a LINE breaks would put two features behind one number again.

**Residual**: SSIM 0.9998 on page two, from sub-pixel glyph positioning on the card's first line
and on the paragraph. Page one is pixel-identical.

What to look at: whether page one ends with the spacer alone. Three lines of the card at the foot
of page one is the break-between-lines behaviour returning; a sliver of the card there with the
whole card also overleaf is the painter culling on the wrong edge.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |
| **Page 2** | **Page 2. AE 0.0012 · SSIM 0.9998** |
| <img src="reference_0002.png" width="480"> | <img src="result%23page_0002.verified.png" width="480"> |

