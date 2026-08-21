# inline/text_indent

# inline/text_indent

`text-indent` was read by nothing and reported by nothing, so every indented paragraph in a
document laid out flush and the difference was invisible to the sink. It is now applied to the
first line of a block container, which is the only line it applies to.

Three arrangements, because the property has three separable behaviours:

- **`#prose`** — declared on the container and inherited. Both paragraphs are indented, and the
  second line of each is not. Applying it where it is declared rather than where the line is
  generated indents the first paragraph only, which is the failure `body { text-indent: 2em }`
  would produce in a real document.
- **`#hanging`** — a negative value, which puts the first line outside the content box. The
  container carries a left margin so the hang has somewhere to go.
- **`#centred`** — an indent NARROWS the band rather than shifting it, so the centred first line
  is centred in what is left and not merely pushed right by the whole indent.

The intrinsic widths carry it too, which nothing here measures: a table cell or a shrink-to-fit
float would otherwise be sized without room for the indent and wrap a line that was supposed to
fit.

What to look at when it moves: the second paragraph of `#prose` flush with the first line's edge
is inheritance lost. Every line indented is the property being applied per line rather than per
block. `#centred`'s first line pushed right by the full 60px is the indent shifting the band
instead of narrowing it.

**Boxes**: 9 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0005 · SSIM 0.9999** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

