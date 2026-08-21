# ua/hr

# ua/hr

`<hr>` appeared nowhere in the corpus, despite having two pieces of machinery of its own:
`UserAgentStyles.Corrections` gives it `margin: 0.5em auto`, and `UnsupportedCss.BorderStyles`
exempts it by name from border-style reporting, because AngleSharp's default sheet makes every
plain `<hr>` `inset` and a page carrying no CSS at all would otherwise report four times.

Both were unmeasured. This scenario measures the first two directly — the half-em margins above
and below, and the automatic horizontal margins centring `#narrow` — and the third by having an
`<hr>` in the corpus at all, since `DiagnosticTests.TheCorpusReportsNothing` runs over every
scenario and would report a border style if the exemption were removed.

The rule itself is a zero-height box drawn entirely by its border, which is why it is visible at
all. This engine paints every border style solid, and the geometry of all seven boxes is exact, so
the 0.9911 is the shading of Chrome's `inset` against a solid line and nothing else. It is the
residual to expect here rather than a regression.

What to look at when it moves: `#narrow` against the left edge is `margin: auto` not resolving on
an `hr`. A rule touching the paragraphs above and below it is the half-em margins missing. A
change in the line's thickness or vertical position is the border, which is the whole box.

**Boxes**: 7 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0023 · SSIM 0.9911** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

