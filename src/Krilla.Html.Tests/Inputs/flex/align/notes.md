# flex/align

The cross axis: `align-items`, `align-self`, stretching and its bounds, baseline alignment, and the
auto margins that outrank all of them. Geometry-exact on all 24 boxes and pixel-identical to Chrome.

- **`#stretch`** is the initial value, and it is why a row of cards comes out level however much
  text each holds. The two one-line items are as tall as the two-line one.
- **`#items`** places fixed-height items in a taller line three ways, with one item overriding its
  container through `align-self`.
- **`#baseline`** lines up three different font sizes on their first baselines. The row's height is
  then the deepest baseline plus the furthest descent BELOW any baseline — a sum no single item
  accounts for, which is the same rule a table row's height follows reached from a different
  specification. `#k4` has no line at all, so its baseline is synthesised from its border box and it
  hangs from the shared baseline by its whole height; without that rule an empty item drops out of
  the group and the row is a different height.
- **`#margins`** is the precedence. An auto margin on the cross axis absorbs the line's leftover
  space and beats `align-items`, which is set to `flex-start` here precisely so that honouring the
  margin and ignoring it give different answers.
- **`#capped`** is the clamp on stretching. A stretched cross size is still bounded by the item's
  own `max-height`, and omitting that clamp costs nothing visible until it matters: without it
  `#p1` fills the container and the declaration reaches nothing at all.
