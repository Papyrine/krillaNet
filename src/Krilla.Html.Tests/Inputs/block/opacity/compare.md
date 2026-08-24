# block/opacity

# block/opacity

Pixel-identical to Chrome, and the five rows are five different reasons that is harder than it
looks. `opacity` is not a fill alpha, and every row here is a case where treating it as one gives a
different answer.

- **`#single`** is the row a fill alpha would also get right, and it is here so the rest have
  something to be compared against.
- **`#group`** is the one that settles the model. Two tiles of the same colour overlap inside one
  half-opaque parent, and the overlap comes out the SAME shade as the rest. Fading each fill on its
  own darkens it. So the box and everything under it are drawn into a group and the group is faded,
  which is what a stacking context is for.
- **`#nested`** multiplies: half of a half is a quarter, which follows from the groups nesting
  rather than from any arithmetic written here.
- **`#zero`** is invisible and still occupies its line, the same distinction `block/visibility`
  draws — `opacity: 0` is not `display: none`.
- **`#order`** measures the consequence nobody expects. A box with opacity below 1 leaves its
  parent's phases and paints with the positioned content, after every in-flow background and line
  on the page. So the faded red, written FIRST, covers the opaque green written after it, and the
  overlap is red-over-green rather than plain green. Document order gives the opposite answer and
  looks entirely reasonable until this row is rendered.

Two things in the implementation follow from the measurements rather than from a reading.

The group has to be ISOLATED as well as faded. Without the isolation the alpha applies to each
drawing operation as it goes down instead of to the finished group, and `#group`'s overlap darkens
— which is exactly the difference the row exists to catch, so it would have failed loudly rather
than quietly.

And `PaintContext` recurses only for a box that establishes a context. A positioned box's own
positioned descendants are flattened to the page by the walk that found it, which is Appendix E's
rule and what the corpus already relied on; recursing there paints every one of them twice. That is
how this first ran, and `position/absolute`, `position/anchors`, `position/fixed` and
`page/absolute_break` all reported it at once — a useful reminder that the existing scenarios are
the safety net for a change to the painter's spine.

A float or an inline-block carrying opacity is faded where it is painted rather than hoisted to the
positioned layer. CSS puts it in the same step as the boxes here, so the two differ only when
something overlaps such a float, which no scenario measures.

What to look at: `#group`'s overlap and `#order`'s. A darker band across `#group` is the isolation
gone; a plain green overlap in `#order` is the box painting in document order.

**Boxes**: 12 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

