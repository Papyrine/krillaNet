# block/border_styles

# block/border_styles

Dashed, dotted and double, per side and at two widths. All three were recognised and painted solid,
which is a quietly wrong render: the box is in the right place with the right colour, and only the
character of the frame is gone.

None of the geometry is specified. CSS says a dashed border is "a series of square-ended dashes"
and leaves every length to the user agent, so the numbers were measured out of Chrome:

- A **dash** is twice the border's width, with a gap of its width — a period of three times the
  width. An 8px border repeats every 24 pixels, 16 on and 8 off; a 3px border every 9. But that
  period is only what the pattern ASKS for: the gap is then adjusted so that a whole number of
  dashes fits the side and the last one ends on the corner. Two counts bracket the side — the most
  dashes that fit at the requested gap, and one more — and whichever leaves a gap closer to the one
  asked for wins. `#dashed`'s 266px side takes 30 dashes at a gap of 2.97 rather than 29 at 3.29,
  and `#thick-dashed`'s 276px side takes 12 at 7.64 rather than 11 at 10.
- A **dot** is the border's width across and repeats at twice it, and its SHAPE follows its size. At
  three pixels and below Chromium draws a crisp square snapped to the pixel grid — measured at ten
  widths, a 1, 2 or 3px dot comes back with no antialiased pixel anywhere in it — and from four
  upward it draws a genuine antialiased circle. Neither approximates the other, so both are drawn.
  `#dotted` is 3px and `#thick-dashed` is 8px so that the width-dependent half of both rules is
  exercised rather than assumed.
- A **double** border is two bands each a third of the width with a third-width gap, which is why
  6px reads 2-2-2 down a column of pixels and why `border: 1px double` is indistinguishable from
  solid.

A patterned edge is drawn along its own centre line rather than as a mitred trapezium. A browser
does not mitre these — dashes run past the corner and a double border's two bands span the whole
side — so the trapezium's purpose, joining two colours cleanly on a diagonal, does not apply.
`#mixed` is what keeps the two paths honest: three patterned edges and one solid on the same box,
so the solid edge still mitres while the others do not.

**Residual**: SSIM 0.9995, and it is confined to the VERTICAL dotted edges. Every horizontal edge in
the scenario is exact — dashes and dots alike, corner to corner, at both widths — and the dots on
`#dotted`'s left and right sides are not.

A horizontal dotted edge fits its pattern into the whole side, corner to corner, and the flush rule
above reproduces it exactly. A vertical one does not: on a 30px box Chromium's left edge carries
five dots at a pitch of 6.25 starting a pixel below the top corner, where the same rule applied to
the full side gives six at 5.4. It is a different construction rather than an offset — probed at
four heights, with and without adjacent horizontal borders, and no inset of the side reproduces it.
The end of such an edge carries a solid square that no dot in the sequence accounts for, which is
the shape of Blink filling a rect at each endpoint of a dashed line and dashing between them.

What to look at: dash and dot SIZE and spacing along the HORIZONTAL edges, which should match
exactly. A difference there is a real regression; a difference on a left or right dotted edge is
the residual above.

**Boxes**: 7 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0002 · SSIM 0.9995** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

