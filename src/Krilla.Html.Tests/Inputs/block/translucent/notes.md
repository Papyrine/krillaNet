# block/translucent

`rgba()` on the colour properties that were drawn fully opaque: the four border sides, the outline,
a collapsed table's grid lines, and a text decoration. `color` and `background-color` already
honoured their alpha, and the rest were reported through `OnDiagnostic` instead.

The reason they were all opaque is one line of the interop: `Krilla.Color` has no fourth channel,
because krilla models opacity as a property of the FILL rather than of the paint — which is the
right model, since it lets one colour be drawn at two opacities without allocating a second paint.
So the alpha has to travel beside the colour, and every property that carries one needs somewhere
to put it.

The backdrop is a saturated yellow rather than white, deliberately. Over white a translucent colour
is merely a lighter version of itself and an opaque render looks like a slightly wrong shade; over
this it is a different colour entirely, so a property that lost its alpha is unmistakable.

## The rows

- **`#border`** is a uniform translucent border, which takes the one-ring shortcut — four mitred
  trapezia would each antialias against their neighbours on the diagonal and composite to more than
  the alpha asked for.
- **`#dashed`** is a patterned edge, whose ink comes from a STROKE rather than a fill. One side
  only: a patterned edge runs corner to corner instead of mitring, so four of them overlap where
  they cross and a translucent one doubles its own alpha at each corner. That is a fact about the
  construction rather than about the colour, and giving the row one side keeps this scenario
  measuring the colour.
- **`#sides`** gives the four sides four DIFFERENT alphas, which is what forces the mitred path:
  the ring shortcut is only available when all four agree, and it now has to agree about the
  opacity as well as the colour. Without this row an implementation that read one alpha and applied
  it to all four would pass.
- **`#ring`** is an outline, which is drawn as a single wound path and so has no seam at all.
- **`#rule`** is a decoration with its own `text-decoration-color`, and **`#current`** is one with
  none — so its rule takes the element's `color` AND that colour's alpha. The two are separate rows
  because the decoration's opacity follows the DECORATION rather than the text: an element
  declaring a translucent underline and then a different `color` for its words draws the rule at the
  opacity the rule was given.
- **`#grid`** is a collapsed table, whose rules belong to the table rather than to the cells either
  side of them and are painted from a list of their own. They needed the alpha carried through that
  list as well as through the cells' styles.

**Residual**: SSIM 1.0000, and the `AE` is high and means nothing — the same situation
`block/gradients` records. Almost every pixel in the scenario is a translucent colour composited
over a backdrop, and this engine and the browser round that composite differently by ONE of 255
across large flat areas. `AE` counts any nonzero difference, so a scenario that is visually exact
reports a large one.

Two places differ by more than that, both of them constructions rather than colours: twenty-four
pixels on `#sides`' corner diagonals, where two antialiased trapezia meet and composite to more
than either alpha — the same shortfall `block/bevelled_borders` records — and the junctions of the
table's grid lines, where the crossing lines deliberately overlap so that no corner is left
unpainted.

What to look at: whether each row's colour is the browser's colour at all. A row that comes out
saturated is its alpha being dropped; a row that comes out too dark is an alpha applied twice.
