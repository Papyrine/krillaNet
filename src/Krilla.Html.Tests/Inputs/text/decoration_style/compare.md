# text/decoration_style

# text/decoration_style

The colour and the rule style of a text decoration, both of which were read and neither honoured: a
coloured underline came out in the colour of the text and a dashed one came out solid.

`text-decoration-color` is the simpler half and is exact. `#coloured` draws its rule in `#c02020`
and the row is one solid span of it. The colour inherits alongside the decoration itself, since a
rule declared on an ancestor is drawn through its descendants and carries the colour of that
ancestor with it. An element that starts a decoration of its OWN starts its own colour with it,
which is what keeps a nested element from picking up a colour it never asked for.

The three styles were measured out of the reference rather than derived, and none of the numbers is
obvious:

- **`double`** is two rules of the drawn thickness with twice it between them, so a 1px underline at
  baseline+1 puts its second line at baseline+4. Two lines 2px apart is the arithmetic a reading of
  the specification suggests, and it is not what Chrome draws.
- **`dashed`** is six pixels on and four off, and **`dotted`** is two on and two off, under an
  underline that is one pixel thick when solid. Both are multiples of two rather than of one, which
  is what says the patterned rule is drawn at TWICE the solid thickness. The Blink pattern is three
  widths on and two off for a dash and one width each way for a dot, and both of those land exactly
  on the measured numbers at a width of two.
- The patterned rule is CENTRED on the position of the solid rule rather than hanging below it.
  Getting that wrong put it a row low and was the whole of the difference between 0.9989 and 0.9999.

`wavy` is reported and drawn solid.

**Residual**: SSIM 0.9999, and the remaining pixels are one thing. Chrome interrupts an underline
around a descender, which is `text-decoration-skip-ink` at its default of `auto`. Sixteen pixels in
`#plain`, at the `p` of "plain", the comma, and the `p` of "comparison". Implementing it needs the
glyph outlines rather than the advances, and it is not reported: it is a default rather than a
declaration, so a report would fire on every underlined document ever converted.

What to look at: the gaps in the Chrome underline under each descender, and their absence in ours.

**Boxes**: 14 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0002 · SSIM 0.9999** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

