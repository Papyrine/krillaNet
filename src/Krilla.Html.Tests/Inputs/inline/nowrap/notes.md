# inline/nowrap

`white-space: nowrap` on an inline element suppressed nothing. The line breaker read the BLOCK's
value — one `var wraps = box.Style.Wraps` at the top of the fill loop — so the property worked only
when it was set on the paragraph itself, which is the rarer half of how it is written. A held phrase
inside a sentence is the common reason anyone reaches for it, and that case did nothing.

The property inherits and applies to the element, so the answer has to come from the token AT the
opportunity: from the pending space where a space offers the break, and from the token itself where
it offers one of its own. That is what the intrinsic-width pass already did — it read
`token.Style.Wraps` while the layout pass read the block's — so the two halves of the engine
disagreed about where a line could break, and only the one nothing measured was right.

- `#moved` is the plain case: the phrase does not fit in the room left, and moves down whole.
- `#overflows` is a phrase too long for any line, which overflows rather than descending forever —
  the same answer a single long word gets.
- `#around` says the suppression is local: the words either side still break as usual.
- `#nested` sets it back on a descendant, which wraps again.
- `#cut` puts `overflow-wrap: break-word` on a held word. The two do not interfere: a held phrase is
  not a word, and the cut still happens inside the word.

`UnbreakableWidths` had to follow. It measures the run that has to move together, and it ended a run
at every space — which is right only where a space is an opportunity. Inside a `nowrap` element a
space is content like any other, and a run that stopped there was measured short of the group it was
meant to hold.
