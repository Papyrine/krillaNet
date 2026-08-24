# inline/vertical_align

# inline/vertical_align

Seven keywords, each aligning an inline-block of a known height so the offset is readable straight
out of the reference geometry rather than having to be backed out of a font's ascent. The boxes are
EMPTY on purpose: an inline-block holding a line of text takes that line's baseline, which would
fold the inherited line-height into every number here, while an empty one falls back to its bottom
margin edge.

Only the table-cell half of `vertical-align` was honoured before. `<sub>` and `<sup>` sat flat on
the baseline, which is ordinary prose rendering wrongly rather than an exotic gap, and nothing
reported it.

The line is 32px on a 16px font, so the baseline sits 21px down — floor the half-leading, then add
the whole-pixel ascent, which is what this engine already did for line boxes and what the browser
turns out to agree with here. Against that baseline:

- **`text-top`** puts the box top at baseline − ascent, and **`text-bottom`** the box bottom at
  baseline + descent, both using the ROUNDED metrics. The measured offsets are exactly 7 and 4.
- **`middle`** puts the box's midpoint half an x-height above the baseline, and reads the x-height
  UNROUNDED — the ratio holds at 0.5283 of the size at 16, 24 and 32 pixels, which is this face's
  `sxHeight` over its em.
- **`top`** and **`bottom`** pin to the line box rather than the baseline, so they are resolved
  after everything else on the line has been counted.
- **`super`** and **`sub`** are user-agent defined, so — as with list markers and
  `line-height: normal` — there is no correct value to compute and agreeing with the browser is the
  only useful target.

The super and sub offsets were measured across three font sizes, and the answer is not a font
metric. They are linear in the font SIZE with an intercept of exactly one pixel: `size / 3 + 1`
raised and `size / 5 + 1` lowered. The OS/2 table's own superscript offset for this face is 7.63px
at 16px where the browser uses 6.33, so reading the font would have been confidently wrong.

Two further things were measured because they are not obvious:

- The offsets use the PARENT's font, not the aligned box's own. Giving the box its own
  `font-size: 10px` inside a 32px paragraph moves it not at all — which matters, since the default
  stylesheet makes every `<sup>` smaller than its parent.
- Chrome holds lengths on a 1/64 pixel grid and truncates onto it, so a superscript offset of
  16/3 + 1 is stored as 6.328125 rather than 6.3333. That is a fortieth of a pixel and it was still
  visible: the paragraph background painted to the line's fractional bottom row came out a
  different shade. Quantising the offsets took the page from SSIM 0.9969 to 0.9989.

`vertical-align` is INHERITED here, which CSS does not do, because the user-agent sheet gives a
table `middle` and its cells `inherit` and a cell can only read the value by being handed it. The
cost is that every run of text inside a cell also arrives carrying `middle`, and line layout must
not act on it. So the inline half applies only where the value was DECLARED and only to a token
that is not the block's own text — two guards, both needed, and without them every table scenario
in the corpus moves at once.

**Residual**: SSIM 0.9989. One row of one paragraph, where a box edge lands on a fractional pixel
and the browser's background fill snaps to a whole one — the same cause as `table/spacing_borders`
and `image/inline_flow`.

What to look at: the offsets, which are the whole assertion. `super` and `sub` are the two that
cannot be derived and so are the two most likely to drift.

**Boxes**: 16 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

