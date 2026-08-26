# block/counters

CSS counters: `counter-reset`, `counter-increment`, `counter()` and `counters()`. Geometry is exact
and the page reads SSIM 0.9999, the residual being glyph edges.

The counters are held by the box builder and mutated as it walks, for the same reason
`ListNumbering` is: a counter's value depends on which elements were VISITED before it, which is a
property of the walk rather than of the element. Deriving it from the DOM afterwards would have to
redo the walk, and would have to redo it identically or the numbers would disagree with the boxes.

**Scoping is the whole of the difficulty.** A `counter-reset` creates a NEW counter that lives until
the declaring element's subtree ends, nested inside any counter of the same name outside it — so
`counter()` reads the innermost and `counters()` reads all of them. `#outer` is the idiom that needs
it: reset on each list, increment on each item, and `counters(section, ".")` to join every level,
giving `1`, `1.1`, `1.2`, `2`. Popping the scope when the subtree ends is what stops a second list
continuing the first one's numbering.

Three smaller rules, each with a row:

- **The reset applies before the increment**, which is CSS's order and observable: an element doing
  both to one counter gives 1 rather than 0. That is how a numbered heading restarts its own
  subsection numbering.
- **`counter(chapter, upper-roman)` has to produce the numeral a list marker of that style would.**
  `#roman` resets to 7 and reads `VII`, through the same formatter `ListMarkers` uses — two
  formatters would eventually disagree and number one document two ways.
- **A counter never reset and never incremented reads zero.** `#absent` shows `[0]`, which CSS
  requires and which is what makes `li { counter-increment: item }` work with no reset anywhere.

What this does NOT model is the specification's rule that a sibling's reset REPLACES rather than
nests at the same depth. The difference shows on a document that resets one counter twice at one
level and reads it with `counters()`, where this produces an extra level. Nesting by subtree is what
the common structures — a list of lists, a document of sections — actually want.

The scenario sets its margins by ID rather than by element name, which is the AngleSharp origin trap
rather than a preference: a bare `ol` selector loses to the user-agent `ol ol { margin: 0 }` that a
nested list matches, because AngleSharp compares specificity across cascade origins. Written as
element selectors, the nested list lost its bottom margin here and kept it in Chrome, and every box
below came out 8px high.

What to look at: the `1.1` and `1.2` prefixes. A flat `1` there is `counters()` reading only the
innermost scope; nothing at all is the comma being lost in the argument split.

## counter-set

Added later, with the property. `counter-set` was read by nothing, so a document numbering a run and
then correcting it mid-way carried on from wherever the increment had reached.

It differs from `counter-reset` in the half that is not the value: it creates no SCOPE. `#set` is
inside a `#scoped` div that resets the counter, and reads `[7]` rather than `[3.7]` — one level,
where a reset would have nested a second inside the first. That is the whole of the difference and
the reason the two do not share a branch.

`#ordered` puts all three on one element. CSS's order is reset, then increment, then set, and it is
observable: reset to 1, increment by 5, set to 9 ends on 9 rather than on 6, and any other order
gives a different number. `#flat` is what the property is actually for — a run of paragraphs
numbered 1, 20, 21, with the middle one setting the counter it also incremented.
