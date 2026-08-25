# block/percent_height

A percentage `height`, `min-height` or `max-height`, which resolved as `auto` however definite the
containing block was. That was correct whenever the containing height is indefinite — which it is
throughout a paginated document, and which is why it survived — and wrong the moment a box declares
a height and something inside it asks for a share of it.

CSS 2.1 §10.5 makes the percentage resolve against the containing block's height when that height is
*explicitly specified*, and behave as `auto` when it is not. Both halves are measured here.

## The rows

- **`#fixed`** is the plain case: a 200px frame, a child at `50%`, and 100px of it.
- **`#loose`** is the control, and the reason the property could be left unimplemented for so long:
  its frame has an auto height, so the child's `50%` behaves as `auto` and it is one line tall. An
  implementation resolving the percentage against something — the page, the content, the viewport —
  gives a different answer here and the same answer as this one everywhere else.
- **`#nested`** chains it. The middle box's height came from a percentage, which makes it definite
  in turn, so its own child gets a quarter of the frame. Resolving only against a DECLARED length
  stops at the first link and leaves the inner box one line tall.
- **`#padded`** and **`#bordered`** resolve against the frame's CONTENT height. The frame's border
  box is 240 and 230, and the child is 100 in both — a percentage of the border box would give 120
  and 115.
- **`#clamped`** and **`#floored`** are `max-height` and `min-height`, which take a percentage the
  same way and are resolved in the same place. 300% capped at 40% is 80px; 10px floored at 30% is
  60px.
- **`#absolute`** is the case that is definite even when nothing declared a height: an absolutely
  positioned box's containing block is a rectangle that has already been laid out, so a percentage
  height on one always has a basis. Its frame declares 200 here, but the rule does not depend on
  that.

- **`#atomic`** and **`#celled`** put the percentage inside an inline-block and inside a table cell,
  both with a definite height of their own. Neither needed anything: a box with a DECLARED height
  settles its own before its subtree is laid out, so what it passes down does not depend on what was
  passed to it.
- **`#atomic-half`** and **`#floated`** are the two that did. An inline-block and a float each resolve
  their OWN percentage height against the block that holds them, and both are laid out through paths
  that threaded no containing height — so both came out one line tall where 100px belongs. They are
  the reason the height goes down alongside the width in `InlineLayout` and `PlaceFloat` rather than
  only through `LayoutChildren`.

The answer has to exist BEFORE the subtree is laid out, which is the whole shape of the change: a
definite height is one the box was told — declared, resolved against its own containing block, or
handed down by an absolute box's offsets — rather than one its content came to, so it can be settled
early and passed down.

Exact on all 32 boxes, and all three pages are pixel-identical.
