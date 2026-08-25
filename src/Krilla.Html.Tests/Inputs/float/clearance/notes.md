# float/clearance

`clear` in the arrangements where clearance and margin collapsing interact, and the defect one of
them found — which turned out not to be about `clear` at all.

`float/clear` measures where a cleared box lands. This measures what happens to the MARGINS around
it, which CSS 2.1 §9.5.2 and §8.3.1 settle between them.

## The rows

- **`#collapsed`** is the ordinary case: a 40px bottom margin and a 20px top margin collapse to 40,
  which would put the box 64px down, and the float's bottom at 100 wins. Clearance is the 36px
  difference and the border edge lands exactly on the float's bottom edge — the collapsed margin is
  absorbed rather than added to it.
- **`#unaided`** is the row the engine was suspected of getting wrong: a cleared box whose own
  150px top margin already carries it past the float. Clearance is then ZERO and the margin stands
  in full, so the box sits at 174 rather than being pulled back to the float's bottom at 100.
  Measured, and it already agreed — which is the useful half of the result.
- **`#through`** has a float short enough that the collapsed margin clears it unaided, so nothing is
  introduced and the box after it collapses normally.
- **`#nofloat`** is the control with nothing to clear at all.
- **`#selfcollapsing`** and **`#plain`** are the two that found something, and only the second
  mentions `clear`.

## What they found

An EMPTY box between two boxes with margins had its margins applied TWICE. With a 40px margin above
it and a 50px margin of its own, everything after it sat 90px down where 50 belongs.

CSS 2.1 §8.3.1 calls this collapsing *through*: a box with no height, no border and no padding does
not SEPARATE the margin above it from the margin below, so the two join one collapsed set that is
applied once. The box itself keeps the position the partial collapse gave it — which is what a
browser reports for it, and what `#plain`'s middle row pins at 64 — and the flow position returns to
where the margin started, so the box after it lands at 74 rather than 114.

`#selfcollapsing` is the same box carrying `clear`, which matters because **clearance takes a box
out of that rule**: §8.3.1's own wording is that two margins are adjoining only when no clearance
separates them, so a box that took any keeps its margins apart the way a box with a border does.
Here the float is short enough that no clearance is introduced, so it collapses through like any
other empty box — and an implementation that keyed the rule on the DECLARATION rather than on the
clearance actually taken would get this row wrong.

Nothing in the corpus held an empty box between two boxes with margins until this scenario did,
which is why a defect this plain survived: it needs a box with nothing in it, and every scenario
written before this one had something in every box.

Exact on all 27 boxes and pixel-identical.

## What is still not done

A cleared box that is its parent's FIRST child still collapses its top margin out through the
parent's top edge, where CSS 2.1 §8.3.1 stops it — "the top margin of an in-flow block element
collapses with its first in-flow block-level child's top margin if the element has no top border, no
top padding, **and the child has no clearance**". Measured at 8px on a frame whose first child clears
a float inside it, and left out of this scenario rather than committed failing.

It is a two-pass problem rather than an oversight. Whether that child HAS clearance depends on where
the float ends, the float is placed while the parent is laid out, and the parent's position is
already settled by the margin this rule would change — which is exactly why §9.5.2 is written in
terms of a *hypothetical* position.
