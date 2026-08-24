# inline/hyphen_breaks

# inline/hyphen_breaks

Where a dash offers a line break. Each box is 120px holding about 165px of text, so one line means
no opportunity was taken and two means one was — which makes every row a yes-or-no answer rather
than a measurement to interpret.

It exists because the engine had no opportunity anywhere but a space. A hyphenated word was one
unbreakable token, so `page-break-inside` in a narrow column overflowed where a browser wrapped it
after `page-`. That was found by the `page/break_inside` scenario, whose card text happened to
contain the word, and reworded out of it so that scenario measured one thing.

The rules here were measured out of Chrome one arrangement at a time rather than read off UAX #14,
and the reason to bother is that the obvious exceptions turn out not to exist:

- **A hyphen between digits breaks.** `1234567890-1234567890` wraps. There is no numeric-context
  rule to implement, which the specification's own numeric sequence rules would have suggested.
- **A LEADING hyphen breaks**, leaving the dash alone on the line above with the whole word below
  it. Suppressing that is the other exception a careful reading suggests, and it is wrong.
- **A hyphen followed by digits breaks**, likewise.
- **A run of dashes breaks after the LAST of the run**, and that needs no rule of its own. Every
  dash offers an opportunity and greedy line breaking takes the last one that fits, so `a--b` keeps
  both dashes on the line above by arithmetic rather than by special case.
- **A dash with nothing after it offers nothing.** The `trailing` row stays on one line.
- **The en and em dashes break; U+2011 and the solidus do not.** The non-breaking hyphen not
  breaking is the whole of its purpose. The solidus is worth stating because a URL is the obvious
  thing a reader expects to wrap, and Chrome does not wrap one.

`&shy;` is absent because it is not implemented. It is a break opportunity in a browser AND paints
a hyphen only when the break falls there, which is a conditional glyph rather than a break rule.

Geometry and pixels are both exact against Chrome.

What to look at: the line count per row, which is the whole assertion. `non-breaking` or `solidus`
growing to two lines means the dash set has been widened too far; `plain` collapsing to one means
splitting stopped happening at all.

**Boxes**: 13 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

