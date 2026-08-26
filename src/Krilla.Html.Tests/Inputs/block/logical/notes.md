# block/logical

The logical box properties, which name a box's edges by their relation to the writing direction
rather than to the page: `margin-inline-start` is the margin at the start of a line, which in a
left-to-right horizontal document is the left one.

They were honoured by nothing and reported by nothing. AngleSharp keeps them under their own names
and expands none of them onto the physical properties, which put them exactly where `word-wrap`
was — and unlike `word-wrap`, they are what NEW stylesheets are increasingly written in rather than
old ones.

This engine has one writing mode and one direction, and reports a document asking for another, so
every mapping here is fixed rather than conditional. Reading them costs a lookup.

## The rows

- **`#margins`** uses the two-value shorthands, read positionally: `margin-inline: 20px 60px` is the
  start edge then the end edge, which is the left and the right.
- **`#edges`** uses the longhands, which most stylesheets write and which beat the shorthand.
- **`#padded`** gives each shorthand ONE value, which applies to both edges of its axis.
- **`#sized`** is `inline-size` and `block-size`, the logical `width` and `height`.
- **`#clamped`** adds `max-inline-size` and `min-block-size`, which clamp the same way their
  physical spellings do and are resolved in the same place.
- **`#inset`** is an absolutely positioned box placed by `inset-block-start` and
  `inset-inline-start`. The `inset` shorthand itself needed nothing — AngleSharp expands that one
  onto `top` and `left` already, which is worth knowing before assuming the whole family is dropped.

Pixel-identical to Chrome and exact on all 14 boxes.

## The one approximation

A LOGICAL declaration wins over a physical one, whatever order they were written in. That is not the
cascade's rule, and nothing here can implement the cascade's rule: the two spellings never reach a
common slot, so there is no way to ask which came later.

It is the right way round all the same. A physical value is present on practically every element of
every document — `* { margin: 0 }` is how a stylesheet begins, and the corpus's own `flatten.css`
does exactly that — so preferring the physical value would make every logical declaration inert,
which is the state this scenario was written to leave. Preferring the logical one is wrong only for
a document that declares the same edge twice in two spellings.

What to look at: whether each box is where the logical declaration asks. A box flush against its
frame is the property not being read at all.
