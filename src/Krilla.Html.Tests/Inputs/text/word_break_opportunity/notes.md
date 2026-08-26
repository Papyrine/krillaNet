# text/word_break_opportunity

`<wbr>`, exact on all 16 boxes at SSIM 0.9999.

It is the only thing HTML has for saying "this word may be split here", which is why a document
holding a long URL or a generated identifier reaches for it. It was in the inline set already and
did nothing at all: it carries no characters, so it reached the tokeniser as an empty run and was
dropped, and a document using it wrapped exactly as one without it.

The frame is 190px so that a word with no opportunity in it OVERFLOWS. That is what makes each pair
differ by a whole line rather than by where a line happens to break, and a scenario measuring the
second is a scenario that passes with the feature unimplemented — the first attempt at this one did
exactly that, with `#none` and `#offered` both two lines tall.

- `#none` and `#offered` — the same twenty-six letters, one of them with a `<wbr>` in the middle.
  Two lines against three.
- `#joined` and `#split` — a URL, which a browser does NOT break at its solidi. One line against
  two.
- `#spaced` — a `<wbr>` immediately after a space, where the opportunity is already there. One
  line, and the row exists to pin that it costs nothing: an implementation emitting a token for the
  element would add its width here.

## The empty rectangle

`getBoundingClientRect()` returns 0,0,0,0 for a `<wbr>` — not a zero-width box where the element
sits, but no box at all. So the box dump reports the same, from the INLINE ITEMS rather than from a
line, because a `<wbr>` produces no token to hang off one. Without that every document containing
one has an element `BaselineHealthTests.EveryElementIsMeasured` counts as unmatched.

## What to look at when it moves

`#offered` back to two lines is the opportunity gone. `#spaced` growing a line is a `<wbr>` that has
started taking advance. And a difference in `#joined` is not this feature at all — it would mean
the solidus had become a break opportunity, which Chrome does not treat as one.
