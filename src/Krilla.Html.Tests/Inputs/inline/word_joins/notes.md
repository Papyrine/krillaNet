# inline/word_joins

The other half of `inline/hyphen_breaks`, and the defect it found. Line breaking used to treat
ADJACENCY as a break opportunity: any two tokens with nothing between them could be split, because
a space was the only thing that ever produced two tokens in the first place. That assumption is
invisible until something else produces two.

`adjacent` is the case that shows it. `<span>abcdefghij</span><span>klmnopqrst</span>` is one word
as far as a browser is concerned — Chrome overflows the 120px box on a single line — and this
engine broke it at the element boundary into two. The same defect covered `<b>` around the first
half of a word, which is ordinary markup rather than a contrived case.

So the fix for hyphens could not be "emit more tokens and let adjacency do the rest": that would
have inherited the bug and rendered `dash-at-join` correctly by accident. A break opportunity is
a property of a token instead — `Token.BreaksBefore` — and every one of the five rows here is a
separate answer to what sets it:

- **`adjacent`** — no opportunity. Two words from two inline elements are one word.
- **`dash-at-join`** — an opportunity, because the first element's text ENDS in a dash. So
  breakability carries across the element boundary even though the boundary itself is not one.
- **`space-at-join`** — an opportunity, from the space, as it always was.
- **`image`** and **`inline-block`** — an opportunity, with no space present. Measured: Chrome
  breaks before an atomic inline that follows text directly. This is the row that keeps the fix
  from over-correcting, since "adjacency is never an opportunity" would wrongly glue an image to
  the word before it.

Geometry and pixels are both exact against Chrome. The `<span>` elements report as unmatched in
the box comparison, which is the corpus's standing behaviour for inline elements rather than
anything this scenario introduced — they generate no box in this engine's tree.

What to look at: `adjacent` is the assertion. Two lines there is the old adjacency rule returning.
Two lines on `image` or `inline-block` becoming one is the over-correction.
