# page/break_avoid

# page/break_avoid

`break-after: avoid`, which asks that a page not begin directly after the box declaring it — the
property every print stylesheet is written around, since a heading stranded at the foot of a page is
what it exists to prevent.

It was recognised and reported rather than honoured, and the reason was structural: `Paginator`
chooses where a page ends by scanning FORWARD from the unbreakable units below it, and `avoid` asks
for a candidate to be rejected in favour of an earlier one. `break-inside: avoid` was already done,
because that one names a rectangle to keep together and the slice already moves rectangles whole.

The scenario is two headings in the same predicament, and the second is what keeps the first honest:

- **`#kept`** carries the property. Its filler ends at 960 and the heading occupies 960 to 1020, so
  the natural break falls at 1020 — the first line of the paragraph after it, which is where the
  page would end with nothing asking otherwise. Chrome moves the break to 960 and the heading
  travels with its paragraph, leaving page one holding the filler alone.
- **`#loose`** is in exactly the same position on page two, with no constraint, and Chrome leaves
  it at the foot of the sheet. Without it an implementation that moved every break away from every
  heading would pass.

## Where the break goes

To the DECLARING box's own top edge, rather than to the nearest earlier break opportunity. Those
two are not the same thing and the difference matters: the nearest earlier opportunity is a LINE
inside the heading, which splits the very box the property was written to keep whole — its
background above the break and its text below.

So the destination is recorded rather than searched for. `break-after: avoid` on a box points at
that box's own top edge; `break-before: avoid` points at whatever precedes the box in document
order. The two chain, so a run of headings each kept with what follows walks back to the first of
them, and a move that would leave a page holding nothing is refused — `avoid` is a preference and a
break has to happen somewhere.

**Residual**: pages one and three are pixel-identical and page two differs only on glyph edges.

What to look at: the PAGE COUNT and where each page begins, which is all this measures — the box
harvest runs against one continuous layout, so the geometry says the same thing whether the property
was honoured or not. Page one holding the heading is the property being ignored; page two holding
no heading is it being over-applied.

**Boxes**: 8 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |
| **Page 2** | **Page 2. AE 0.0045 · SSIM 0.9991** |
| <img src="reference_0002.png" width="480"> | <img src="result%23page_0002.verified.png" width="480"> |
| **Page 3** | **Page 3. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0003.png" width="480"> | <img src="result%23page_0003.verified.png" width="480"> |

