# block/list_position

# block/list_position

`list-style-position: inside` moves the marker onto the item's first line, where `outside` hangs it
before the item's border edge. The box geometry is identical either way — Chrome reports the same
300x24 item for both — so this is a scenario the pixels have to carry, and the geometry comparison
is here to confirm that nothing moved rather than that something did.

The marker's advance is the whole measurement, and it took two attempts to get. Detecting where the
text starts from the INK is confounded by the first glyph's left side bearing, which grows with the
font size and made a clean rule look like a drifting one. Wrapping the text in a span and reading
its own rectangle gives the origin exactly, and then two rules fall out:

- A **counter** takes the advance of `N. ` with its trailing space — the same string the outside
  marker is right-aligned by. It agrees to a hundredth of a pixel at every size because both sides
  shape the string rather than summing raw advances.
- A **symbol** takes `side + font-size + 1`, in whole pixels off the whole-pixel ascent. That is
  not derivable from anything and was measured at six sizes from 12px to 40px, exact at every one.
  No fraction of the em fits: the ratio drifts from 1.375 down to 1.325 across that range, because
  the symbol's own side moves in the uneven steps `SymbolSize` produces. Fitting the 1.375 that
  16px and 24px both give would be a pixel out at 32px and two out at 40px.

The reserved space reuses `text-indent`'s mechanism, which already narrows a block's first line
from its start edge, and the two ADD rather than one winning — an indented inside list item starts
its text past both, which is what a browser does.

`#inside-wrap` is the row that shows what inside really means: a long item's second line starts
under the MARKER rather than under the text above it, because the marker took inline space on the
first line and nothing reserves it on the rest.

`#inside-number` is here because the two marker kinds take their advance from different places, so
a rule that is right for one can be wrong for the other.

Geometry is exact and the page reads SSIM 1.0000.

What to look at: where the text starts on each first line, and where the second line of
`#inside-wrap` begins. Text starting at the content edge is the marker not reserving its space.

**Boxes**: 13 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0002 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

