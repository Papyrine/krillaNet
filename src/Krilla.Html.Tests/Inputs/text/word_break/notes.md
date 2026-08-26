# text/word_break

Pixel-identical to Chrome, and geometry-exact.

Two properties that let a line break inside a word, which every other rule in the line breaker
forbids. They collapse into one value here, because the engine cares what a value PERMITS rather
than which property asked for it. The two permissions differ, and the scenario separates them:

- `#overflow` is the control, with neither property: a word with no opportunity in it overflows its
  box by 95px and nothing breaks.
- `#wrap` has `overflow-wrap: break-word`, which breaks a word only when it fits on no line of that
  width at all.
- `#all` has `word-break: break-all`, which breaks anywhere whether or not the word would overflow.
  On a single long word the two are indistinguishable, which is why the fourth row exists.
- `#legacy` writes `word-wrap: break-word`, which is the same property under the spelling it had for
  a decade before `overflow-wrap` existed and is still what reporting tools and mail merges emit.
  The cascade does not alias the two — it hands `word-wrap` back under its own name and leaves
  `overflow-wrap` empty, exactly as it does with the two break-property spellings — so a document
  written this way broke nothing and reported nothing at all. Without the row the scenario is exact
  either way; with it, and with only the modern spelling read, the word overflows and the page is a
  line short.
- `#mixed` is the row that told them apart, and it found a real defect. With ordinary words before
  the long one, the long word is first MOVED to a line of its own by the ordinary break rule, and
  the code that did so added it to the new line directly, so nothing afterwards ever asked whether
  it could be cut. The paragraph came out one line short and 96px wide. A word moved to a fresh
  line now falls through to the splitting loop instead of being placed.

Splitting walks outward from the token start rather than binary-searching, because `ShapedText`
answers a sub-range by summing advances it already holds: the walk costs what one width query costs
and hands back every candidate on the way. A cut takes at least one character on an empty line
whatever the width, since a character wider than the box would otherwise loop forever.

No hyphen is drawn at the cut. The break was forced by the box rather than offered by the text, and
a browser draws nothing there, which is what separates this from `text/soft_hyphen`.

What to look at: the second line of `#mixed`. If the long word is intact and hanging out of the box,
the split is being bypassed by the ordinary break.
