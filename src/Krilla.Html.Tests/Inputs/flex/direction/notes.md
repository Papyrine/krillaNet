# flex/direction

The four values of `flex-direction`, each with the same three items in the same source order.
Geometry-exact on all 21 boxes and pixel-identical to Chrome.

What changes between them is which edge the items are packed against and which way the main axis
runs — not the order of the boxes in the document, which is what `order` changes and what
`flex/nested` measures separately.

- **`#rrev`** deliberately leaves 190px of slack, because `row-reverse` is indistinguishable from
  "the same row with the list reversed" whenever the items fill the container exactly. It packs
  against the RIGHT edge, so the row ends flush with the container rather than starting flush with
  it.
- **`#crev`** is the same thing vertically: item 1 at the bottom, the stack ending flush with the
  container's foot.
- **`#rrevjust`** is the composition that is easiest to get backwards. A reversed main axis reverses
  what `justify-content` means as well, so `flex-end` is the LEFT edge here — the two compose rather
  than cancelling.

**It found the column bug.** A column container's items were coming out at ZERO width, and only
those with a declared height — which is the arrangement most likely to be written. The cross size
of a column item is its WIDTH, and it was being settled only on the branch that needed it to
measure a natural height; an item whose main size was declared skipped that branch and kept the
zero it was constructed with. Every later step then used it: the layout, the re-layout at the flexed
height, and the placement. `flex/column` did not catch it because every item there is sized by its
content, which is the branch that worked.
