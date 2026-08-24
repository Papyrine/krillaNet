# block/border_styles

Dashed, dotted and double, per side and at two widths. All three were recognised and painted solid,
which is a quietly wrong render: the box is in the right place with the right colour, and only the
character of the frame is gone.

None of the geometry is specified. CSS says a dashed border is "a series of square-ended dashes"
and leaves every length to the user agent, so the numbers were measured out of Chrome:

- A **dash** is twice the border's width, with a gap of its width — a period of three times the
  width. An 8px border repeats every 24 pixels, 16 on and 8 off; a 3px border every 9.
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

**Residual**: SSIM 0.9906, the largest in the corpus after `ua/hr`, and it has one cause. The dash
phase restarts at each corner here, so a side whose length is not a whole number of periods ends on
a partial dash. A browser redistributes the remainder along the side so that it ends flush, which
is not reproduced. It is a visible difference at the end of each side and nowhere else — the dashes
along most of every edge line up exactly.

What to look at: dash and dot SIZE and spacing, which should match along the first two thirds of
any edge. A difference that starts at the first dash rather than accumulating toward the corner is
the period being wrong rather than the phase.
