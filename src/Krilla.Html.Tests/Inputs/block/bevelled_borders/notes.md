# block/bevelled_borders

`inset`, `outset`, `groove` and `ridge` — the four border styles drawn in two derived shades of the
declared colour.

They were reported rather than implemented, because they "need two derived shades", and painted
solid. CSS specifies none of the shading: it says the box should look carved into or
raised out of the canvas and leaves every colour to the user agent. So — as with
`line-height: normal` and every number in `ListMarkers` — there is no correct value to compute, and
agreeing with the reference browser is the only useful target. Everything below was measured.

## The two shades

Both scale the colour by a factor taken from its BRIGHTEST channel, which is what moves the
lightness while keeping the hue, and both truncate through a scale of 255.99998 rather than
rounding. Verified against Chromium at five bases and three border widths:

| Base | Dark | Light |
| --- | --- | --- |
| `#3366cc` | `#1e3c78` | `#3f7fff` |
| `#808080` | `#2c2c2c` | `#d4d4d4` |
| `#cccccc` | `#787878` | `#ffffff` |
| `#dddddd` | `#898989` | `#ffffff` |
| `#ffffff` | `#ababab` | `#ffffff` |

White is hardcoded in Chromium rather than computed, and so is black — one has no brightest channel
to scale down and the other none to scale up at all.

## The rule no derivation predicts

**An undeclared border colour does not use the element's colour.** `border-color: currentColor` —
declared, or left at its initial value, which is what `border: 1px inset` produces — is drawn in a
fixed pair, `#9a9a9a` over `#eeeeee`, whatever `color` says. Four arrangements were measured and all
four agree: `color: gray`, `color: black`, an explicit `border-color: currentColor`, and a plain
`<hr>`. A box that DECLARES `border-color: gray` gets the derivation instead, at `#2c2c2c` and
`#d4d4d4`.

`#current` is the row that pins it, and its `color` is red on purpose: a reading that derived the
shades from `currentColor` would be visibly wrong there rather than subtly so.

That rule is also the whole of what `ua/hr` was missing. It sat at 0.9911 with the note saying the
residual was "the shading of Chrome's `inset` against a solid line" — and the truth was worse and
simpler: `border: 1px inset` sets `border-color: initial`, `initial` was not read as
`currentColor`, so the colour parsed to nothing, `HasBorder` went false, and **the rule was not
drawn at all**. It is pixel-identical now.

## Groove and ridge are not one bevel in two shades

The outer half of a groove is an `inset` edge and the inner half an `outset` one — which puts dark
over light on the top and light over dark on the bottom, and is what makes the carving read. A ridge
is those two exchanged. Shading a single band from light to dark is the plausible reading and gives
a different picture on every side.

## Residual

SSIM 0.9990, and every differing pixel is on a mitre diagonal — two per row, at the corners where
two colours meet. That is the general property of the mitred trapezium path that `PaintUniformBorder`
exists to avoid for a uniform border and cannot avoid here: two antialiased edges meeting on a
diagonal do not composite to full coverage, so a little of the page shows through. Chromium fully
covers the same pixel with a 50/50 blend of the two colours. `#mixed` is where it is most visible,
and it is the same cause `block/border_styles` records for its own mixed row.
