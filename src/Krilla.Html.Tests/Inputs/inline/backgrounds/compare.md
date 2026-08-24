# inline/backgrounds

# inline/backgrounds

A background on an inline element, which was read and not painted — so every highlighted phrase in
every document came out on the paragraph's colour, and nothing said so. The scenario exists because
the corpus had no way to notice: an inline element generates no box, so until text runs started
carrying their element's selector there was nothing for the geometry comparison to match and the
pixels were the only witness.

An inline element's background is **one rectangle per line fragment**, not one around the element.
`#wrapped` is the row that shows why: its highlight breaks across three lines, and a single
rectangle round the lot would colour the blank space at the end of each line and the indent at the
start of the next.

The rectangle is the **font box**, not the line box — the baseline less the whole-pixel ascent, as
tall as that ascent plus the whole-pixel descent. `#leading` gives its paragraph a 40px line height
and the fill stays 17px tall, hugging the text with the leading left uncoloured. Filling the line
box instead is indistinguishable whenever `line-height` is close to the font's own height, which is
most of the time.

`#nested` and `#sized` are the two rows about nesting, and they fail differently:

- `#nested` has a background inside a background. The inner element's text carries the INNER
  element's style, so without recording the ancestor there is nothing to paint the outer one from
  and the highlight has a hole in it exactly where the nested word sits.
- `#sized` has a 24px outer with an 11px inner that has no background of its own. The outer's fill
  behind that word has to be the OUTER element's height — measured against its own font rather than
  the run's — or the highlight narrows to 12px for one word and reads as a rendering fault.

`#clear` is the control: an inline element with no background of its own must paint nothing, which
is what keeps the block's own text from being filled twice over. Text belonging to the block rather
than to any inline element is skipped for the same reason — the block already painted it, and
filling it again is invisible while the colour is opaque and wrong the moment it is not.

The fill is snapped to whole pixels, which is the browser's construction rather than a rounding
convenience. A fragment from 49.81 to 127.21 is filled over columns 50 to 126 with hard edges at
both ends; left as a fractional rectangle it picked up one whole extra column of colour on the
right, because the rasteriser reading the PDF snaps outward where the browser snaps to nearest.
That single column was the whole of the difference between 0.9999 and 1.0000.

Padding and a border on an inline element are still not drawn, and are reported. That is a layout
question rather than a painting one: horizontal padding advances the text after it.

Geometry is exact on all sixteen boxes and the page reads SSIM 1.0000, with the residual confined
to glyph edges at 24px.

What to look at: the left and right ends of each highlight, and the gap between the fill and the
paragraph edge in `#leading`.

**Boxes**: 16 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0002 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

