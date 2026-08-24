# block/shadows

# block/shadows

`text-shadow` and `box-shadow`, both of which were reported and neither painted. Geometry is exact —
a shadow changes no box — and the page reads SSIM 0.9985 with no pixel differing by more than two of
255, the residual being antialiasing on the rounded corner of `#rounded`.

**Offsets only. No blur, and no spread.** Those are two different limits with two different causes,
and the second was found here rather than reasoned about:

- A **blur** needs a Gaussian, which a PDF content stream does not express for an arbitrary shape —
  and a text shadow's blur follows glyph outlines, so no gradient can stand in for it. A blurred
  shadow drawn SHARP is a hard dark copy where a soft halo belongs, so it is not drawn at all. That
  is the rule an unsupported outline style already follows: for decoration with no layout
  consequence, wrong ink is worse than none.
- A **spread** is unreachable rather than unimplementable. AngleSharp ELIDES A ZERO BLUR when it
  serialises the value, so `6px 6px 0 4px` — offset, no blur, spread four — comes back as
  `6px 6px 4px`, which is byte-for-byte what a real four-pixel blur comes back as. The first version
  of this scenario had spread rows and they measured wrong for exactly that reason. A three-length
  value therefore has to be read as a blur: reading it as a spread would draw a hard shadow wherever
  an author asked for a soft one, which is much the worse of the two mistakes.

`#several` is the row that pins the order. Two shadows are painted FARTHEST FIRST, so the layer
written first ends up on top — the reverse of the order they appear in, and invisible until two of
them overlap, which is the only time anyone writes two.

`#translucent` is why this scenario found a second feature. A shadow is behind the box that casts it
INCLUDING behind its own background, so a translucent background shows the shadow through — and
`rgba()` was not honoured at all. `background-color` and `color` now carry their alpha, which
`Krilla.Color` cannot hold: krilla models opacity as a fill property rather than as a fourth channel,
so the alpha travels alongside. Every other colour property is still drawn opaque and is reported.

What to look at: the green band under `#translucent`, which should show through the panel rather than
stopping at its edge.

**Boxes**: 8 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0057 · SSIM 0.9985** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

