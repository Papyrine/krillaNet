# float/overflow_bfc

# float/overflow_bfc

The other half of `overflow`, and the reason anyone writes it. A box with `overflow` other than
`visible` establishes a block formatting context, which has two consequences and this measures
both.

**It is placed BESIDE an outside float rather than overlapping it.** `#beside` is a 120px float and
`#container` comes back from Chrome at x=120 with a width of 696 — its border box narrowed and
shifted, not merely its lines shortened. That is the exception to the most surprising rule about
floats: an ordinary block keeps its full width and lets the float overlap it, which is what
`float/basic` pins with `#block`. A float with a text block beside it is the pre-flexbox way to lay
out a media object, and `overflow: hidden` on the text block is how it was done — so this is the
common case rather than a corner one.

**It grows to contain its own float.** `#inner` is 60px tall and the container comes out 60px tall,
where a container that shared its parent's float context would collapse to the height of its text
and let the float hang out.

Three things were measured rather than assumed:

- The band is sampled at the container's TOP EDGE alone, and the box keeps that width for its whole
  height. The container is 60px tall beside a 90px float and stays 696px wide, so nothing re-widens
  it below the float's bottom.
- `#after`, an ordinary block, is back at x=0 with the full 816px width even though the float still
  extends 30px below the container. Only a box establishing a context avoids a float.
- The probe has to be an infinitesimally thin slice at the top edge rather than a zero-height one.
  `FloatContext.Band` treats its range as half-open, so a zero-height query overlaps nothing and
  every box comes back full width — which is exactly how this scenario failed first time.

Only an AUTO width is narrowed. A declared width is honoured as declared and the box is left where
it is, where a browser shifts it sideways as well. Nothing here measures that, and it is recorded
as a limit rather than as a decision.

Geometry is exact. Pixels read SSIM 0.9999, from glyph positioning on the container's one line.

What to look at: `#container`'s x and width. Back at x=0 with 816px means the formatting context is
not being established; a height of 24 rather than 60 means it is not containing its float.

**Boxes**: 6 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0005 · SSIM 0.9999** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

