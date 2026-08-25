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
- A **dot** is the border's width across and repeats at twice it, and it is ROUND. That is why it
  is drawn as a zero-length dash under a round cap rather than as a square one: the two are
  indistinguishable at 1px and obviously different at 8. `#dotted` is 3px and `#thick-dashed` is
  8px so that the width-dependent half of both rules is exercised rather than assumed.
- A **double** border is two bands each a third of the width with a third-width gap, which is why
  6px reads 2-2-2 down a column of pixels and why `border: 1px double` is indistinguishable from
  solid.

A patterned edge is drawn along its own centre line rather than as a mitred trapezium. A browser
does not mitre these — dashes run past the corner and a double border's two bands span the whole
side — so the trapezium's purpose, joining two colours cleanly on a diagonal, does not apply.
`#mixed` is what keeps the two paths honest: three patterned edges and one solid on the same box,
so the solid edge still mitres while the others do not.

**Residual**: SSIM 0.9938, and every dash in the scenario now lands where Chrome's does — both
dashed rows agree run for run, corner to corner. What is left is the DOTS, and it is a rasterisation
difference rather than a placement one.

Their positions match: floored to whole pixels, Chrome's 3px dots start at 0, 5, 11, 17, 23 … which
is exactly the flush pitch of 5.977 this computes. Their SHAPE does not. Chromium draws a small dot
as a crisp square snapped to whole pixels — three solid pixels across, no antialiasing anywhere in
it — and draws a large one as a genuine antialiased circle, which was measured at 3, 8 and 12px.
This draws a circle at every size, so at 3px it is a soft blob where Chrome has a hard square.

That is why the note above about a dot being ROUND stands: it is right at 8px and wrong at 3, and
the corpus only has a 3px row. A row at 8px would show the two agreeing.

What to look at: dash and dot SIZE and spacing, which should match along the WHOLE of any edge now
rather than only its first two thirds. A difference that accumulates toward a corner is the flush
distribution gone; one that starts at the first dash is the period.
