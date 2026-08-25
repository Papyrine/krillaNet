# block/pseudo_block

`display: block` on a `::before` or `::after`, which was laid out as inline content of the host and
reported. It is the oldest idiom in CSS — `content: ""; display: block; clear: both` on an `::after`
is how a container was made to enclose its floats before `overflow` was used for it — and it
rendered as nothing at all, because an inline pseudo with empty content generates no box to clear
with.

## What made it hard

Not the layout. A block pseudo is a box appended to the host's block children, with the run closed
first so a `::before` lands above the host's own text and an `::after` below it.

What made it hard is that **the cascade cannot say whether the pseudo declared `display` at all.** A
pseudo-element's cascaded style carries the HOST's declarations too, and measured: a `<div>` whose
`::before` declares nothing but `content` comes back with `display: block`, leaked from the
user-agent rule for `div`. So the value the pseudo reports is the value the host has, and `block` is
exactly what they agree on for every block host — which is every host anyone writes a block pseudo
for. Reading it would make every `::before` on every `<div>` a block.

So `display` is recovered from the RULES instead: the stylesheets are scanned for style rules whose
selector names `::before` or `::after`, and the last one matching the element wins. The same shape
as the `@page` recovery, bounded the same way, and with the same caveats — specificity is not
compared and media queries are not evaluated.

The leak is not confined to `display` either. A host with `width: 400px` was handing that width to
its `::before`, which then ignored its own margins because it had been told how wide to be — so the
pseudo's own declarations are now separated out and re-parsed through an element that is never added
to the document, the same route a page margin box takes. That improves an inline pseudo too, which
was silently taking its host's horizontal padding and border.

**No host in this scenario carries a margin**, deliberately: a margin on a host arrives on its pseudo
as well, and the separation above treats a value the two agree on as the host's. The gaps between
the rows are boxes of their own with no pseudo-element on them.

## The rows

- **`#clearfix`** is the idiom itself. Its float is 70px tall and its text is one line, so the
  container encloses the float only if the `::after` exists, is a block, and clears.
- **`#ruled`** has a block pseudo at each end, which is what makes source order observable: the two
  rules bracket the host's text rather than both landing above it.
- **`#mixed`** gives its pseudo margins, padding and a border. They are the BOX's now rather than an
  inline element's — the vertical margins apply where an inline element's would be dropped, and the
  width is what the margins leave rather than the host's.
- **`#sized`** is empty with a height, which is what a spacer pseudo is and what an inline one
  cannot be.
- **`#inline-host`** is the control. Its host is a `<span>`, which contributes runs to a line rather
  than a list of boxes, so a block pseudo inside one has nowhere to go — those stay inline, and are
  still reported.

Exact on all 14 boxes and SSIM 1.0000. The box comparison cannot see a pseudo-element directly —
a browser reports no rectangle for one either — so what it measures is the effect: every host's
height, and the position of everything below.
