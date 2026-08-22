# block/box_sizing

`box-sizing`, which decides whether a declared `width` or `height` measures the content box or the
border box. Every real stylesheet sets `border-box` and until this landed every one of them was
laid out a padding and a border too wide.

The first two boxes carry the same three numbers — 300px of width, 20px of padding, a 5px border —
so the only difference between them is which box the 300 measures. `#content` is 350 wide and
`#border` is 300.

What the rest are for:

- **`#floor`** declares 30px against 50px of padding and border. The content floors at zero and the
  box is as narrow as its own surround allows, rather than going negative. Chrome agrees at 50.
- **`#half`** is `width: 50%`. The percentage resolves against the containing block first and is
  the border box after that, so the box is exactly half the page however thick its border — 408px,
  not 408 plus 60.
- **`#tall`** and **`#least`** are the vertical pair: `height` and `min-height` both measure what
  `box-sizing` says, so a 100px box holds 50px of content and an 80px minimum holds 30px.
- **`#capped`** is `max-width` with auto margins, the standard centring idiom. The maximum clamps
  the BORDER box to 400 and the leftover 416 splits into two 208px margins. It matters that the
  clamp happens before the margins are resolved: clamping a content width instead centres a box
  that is 450 wide.
- **`#atomic`** and **`#floated`** are the two boxes sized outside `ResolveHorizontal` — an
  inline-block is given an assigned width by `InlineLayout`, and a float's width is decided by
  `ShrinkToFit` before it is placed. Both are separate code paths and both had to be told.
- **`#picture`** is a replaced box, where the declared width covers the surround and the aspect
  ratio applies to what is LEFT of it: 120px on the page is 90px of picture, and the 64x32 swatch
  makes that 45px tall for 75px of box.

The float block is last in the source on purpose. A block-level replaced element must not overlap
a float (CSS 2.1 §9.5), so an `<img>` after a float that is still hanging gets pushed to its right
edge in Chrome — and this engine does not push anything aside for a float yet. Written the other
way round the scenario measured that gap instead of this feature, by exactly the float's 240px.

`html` is 801 tall against `body`'s 761 because the root contains the float and `body` does not,
which is the same asymmetry `float/basic` measures.

**Residual**: SSIM 0.9999 on one line — the text beside the float, whose glyph ink differs by a
tenth of a pixel of centroid and 0.16% of total coverage. Sub-pixel glyph positioning, the same
cause as `text/kerning`, and not a geometry difference: every one of the fourteen boxes is exact.
