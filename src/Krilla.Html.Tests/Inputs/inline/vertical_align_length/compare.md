# inline/vertical_align_length

# inline/vertical_align_length

`vertical-align` given a length or a percentage, which the resolver previously read and dropped
onto `baseline` — so a document raising a footnote marker by 4px rendered it exactly where an
unaligned one goes, and nothing said so.

Two things are measured, and the second was not obvious:

- A **length** raises the box off the baseline, so a negative value lowers it. `#raised` at `6px`
  grows its paragraph from 24px to 30px, because the shifted box carries its whole leading box up
  with it and the line grows to contain both it and the strut.
- A **percentage** resolves against the element's own `line-height`, not its font size. `#proportional`
  at `25%` of a 24px line lands in exactly the place `#raised` at `6px` does — same paragraph
  height, same span position, to the pixel. Resolving it against the 16px font size instead would
  give 4px, which is close enough to look like a rounding error and wrong on every line whose
  height is not its font size.

`#relative` is the row that separates a length from the keywords. Every `vertical-align` KEYWORD in
this engine measures against the parent's font, which is what CSS says for `middle` and what
measurement confirmed for the rest. A length does not: `em` and a percentage are ordinary value
resolution against the element that declared them, so a `0.5em` on a 12px span inside a 16px
paragraph is 6px rather than 8px. Its paragraph comes out 29px tall, which is a number neither
reading of the em produces by accident.

`#none` states `0px` explicitly so the rows above have a baseline to differ from, and because
`vertical-align: 0` genuinely is the baseline — which is why an unparseable value has to fall back
to something distinguishable from zero rather than to `CssLength.Zero`, the same trap
`ResolveFontSize` records.

This is also the scenario that made inline text geometry measurable at all. Its six spans generate
no box of their own, so the comparison had nothing to match them against and reported the scenario
as passing while measuring only the paragraphs. Text runs now carry their element's selector and
the box dump unions their fragments, which took the corpus from eighteen unmatched elements to
none.

Geometry is exact against Chrome on all fourteen boxes, spans included.

What to look at: the paragraph heights — 30, 28, 29, 30, 30, 24. They encode every shift on the
page, since each is the line grown to contain a box that moved.

**Boxes**: 14 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

