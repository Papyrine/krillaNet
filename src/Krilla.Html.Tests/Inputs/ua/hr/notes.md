# ua/hr

`<hr>` appeared nowhere in the corpus, despite having two pieces of machinery of its own:
`UserAgentStyles.Corrections` gives it `margin: 0.5em auto`, and `UnsupportedCss` used to exempt it
BY NAME from border-style reporting, because AngleSharp's default sheet makes every plain `<hr>`
`inset` and a page carrying no CSS at all would otherwise have reported four times.

Both were unmeasured. This scenario measures the first directly — the half-em margins above and
below, and the automatic horizontal margins centring `#narrow` — and measured the second by having
an `<hr>` in the corpus at all, since `DiagnosticTests.TheCorpusReportsNothing` runs over every
scenario. The exemption is gone: implementing the four bevelled border styles removed the entry that
would have fired, which is the general lesson it left behind — **an exemption by element name is a
sign the property is unimplemented, not a sign the report is wrong.**

`ua/presentational_text` measures the rest of what an `<hr>` can be told to do, its four
presentational attributes being where a legacy document says it.

The rule itself is a zero-height box drawn entirely by its border, which is why it is visible at
all. It is pixel-identical now, and was not always: the notes here recorded 0.9911 as "the shading
of Chrome's `inset` against a solid line", which was a guess with a plausible cause and the wrong
one. Reading the actual pixels out of the two PNGs found that the rule was not drawn AT ALL:
`border: 1px inset` sets `border-color: initial`, `initial` was not read as `currentColor`, and
`HasBorder` went false. **A residual with a stated cause is still a guess until something measures
the cause.**

What to look at when it moves: `#narrow` against the left edge is `margin: auto` not resolving on
an `hr`. A rule touching the paragraphs above and below it is the half-em margins missing. A
change in the line's thickness or vertical position is the border, which is the whole box.
