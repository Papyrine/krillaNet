# text/white_space_longhands

`white-space` is a shorthand for `white-space-collapse` and `text-wrap` in CSS Text 4, and the two
spellings do not meet in AngleSharp: it expands neither into the other, so the longhands come back
empty for a document that writes the shorthand and the shorthand comes back empty for one that
writes the longhands. A document written in the modern spelling therefore collapsed every space it
had asked to keep — the same shape as `word-wrap` beside `overflow-wrap`, and as
`page-break-before` beside `break-before`.

Both are read now, the shorthand first because a document writing it means it. The five values of
the shorthand are the five combinations this engine distinguishes, which is what lets the longhands
fold onto the same answer rather than needing an axis of their own — the first four rows here are
`pre`, `pre-wrap`, `pre-line` and `nowrap` written the long way.

`#inherited` is the row that decides how an absent longhand is read. It has to fall back to what the
element INHERITED rather than to the property's own initial value: the inner block turns wrapping off and
keeps the preserving that came from its parent, where a fallback to `collapse` would silently undo
the half nobody mentioned.

`break-spaces` is still out of reach from either spelling. AngleSharp drops it from `white-space`,
and `white-space-collapse: break-spaces` is folded onto `preserve` and reported — it differs only in
that a run of trailing spaces may itself be broken.
