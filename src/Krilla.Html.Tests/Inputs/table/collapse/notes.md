# table/collapse

Pixel-identical to Chrome. `border-collapse: collapse` is what most real table CSS asks for, and
it was the last blanket entry in the diagnostic table that a document is likely to hit.

The model is one line per grid boundary rather than one border per cell. Each line's width and
colour are won by one of the boxes touching it, the line is centred on the boundary, and every
box's used border on an edge is HALF the line there — so the table's own box reaches half a line
beyond its outermost cells. Every number was measured:

- **`#basic`** — 2px uniform on 80px cells with 6px padding gives 94px cells inside a 190px table.
  Two 2px borders meeting make a 2px line, not a 4px one, and `border-spacing` stops applying.
- **`#widths`** — a 6px cell against 2px neighbours takes 3px either side of its edges and comes
  out 98px wide against their 96px. The wider border wins the shared edge outright.
- **`#frame`** — a 4px table border against 2px cells wins the outer edges, putting the cells 2px
  inside the table box.
- **`#odd`** — 3px, so every half lands on a half pixel and the cells sit at x=1.5.
- **`#separate`** — the same markup under the separated model, so the difference between the two is
  the measurement rather than the look of either.

Three things in the implementation are load-bearing.

**The halving runs before anything measures a cell.** The column algorithm sizes columns from cell
border-box widths, so the halved borders have to be in the cells' styles by the time it looks;
resolving afterwards would size the table with one set of borders and paint it with another.
Rewriting the styles is also what lets the rest of table layout stay untouched — a cell whose style
says it has a 1px left border behaves like any other such cell.

**The lines are painted once, not as two halves.** `#odd` is in the scenario for that reason: two
cells each drawing their own 1.5px half meet on a half pixel and antialias into a visible join down
the middle of every line. Drawing the boundary once has no seam at any width.

**The wider line owns a crossing.** Every line runs half a crossing line past each end so no corner
is left unpainted, which makes the junctions overlap, and the list is sorted by width so the wider
line goes down last. Measured: where `#frame`'s 4px green table border crosses a 2px red row line,
Chrome shows green. Painting the horizontals last regardless — the obvious way to fill corners —
puts red on all four corners of that table and was how this was found.

One thing it cost, and it is worth knowing because it will happen again. Replacing a cell's
`ComputedStyle` broke the reference identity that line layout uses to tell a block's own text from
an inline box of its own — a text node takes its parent's style INSTANCE, and that shared reference
is what keeps a cell's inherited `vertical-align: middle` from shifting the cell's text. Every
collapsed cell came out 0.77px too tall, which is exactly half an x-height off the line box.
`CollapsedBorders.Rewrite` repoints the inline items when it replaces a style, which restores the
invariant rather than weakening the guard.

The one gap is `border-style: hidden`, which CSS gives absolute priority at a shared edge — it wins
even against a wider border. `StyleResolver` folds `hidden` into a zero width before anything
downstream sees it, so by the time the edges are resolved it is indistinguishable from an absent
border and loses on width. It is reported rather than left silent, and only inside a collapsed
table, since everywhere else `hidden` and `none` really are the same thing.

What to look at: the corners of `#frame`, which is where the crossing rule shows, and the middle of
any line in `#odd`, which is where a seam would.
