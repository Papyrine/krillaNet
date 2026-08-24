# block/border_radius

# block/border_radius

Rounded corners, on a background alone and on a border, circular and elliptical, per corner and
clamped. They were painted square before, which is one of the more visible gaps left — a rounded
card is ordinary markup rather than a flourish.

Pixel-identical to Chrome, and that is a consequence of one decision. A quarter arc is drawn as a
cubic bezier with its control points 0.5522847498 of the way along each tangent, which is the
standard circle constant and what every renderer uses, browsers included. Matching the browser's
CONSTRUCTION is what makes the pixels agree; a more accurate arc would agree less. That is the same
lesson `ListMarkers` records about stroking a circle rather than filling an annulus.

Five rows, each measuring something the others cannot:

- **`#background`** is a radius with no border, so the shape is the fill alone.
- **`#bordered`** adds a 4px border, so the ring has to be an outer rounded rect with an inner one
  cut out of it. Each inner radius is the outer one less the edge it runs along, floored at zero —
  which is what makes a thick rounded border read as a ring rather than a tube.
- **`#elliptical`** uses the slash form, `30px / 12px`. A corner carries two radii rather than one
  for this reason: treating a corner as a single radius is right for almost every document and
  wrong the moment anyone writes the slash.
- **`#corners`** rounds two corners and leaves two square, which is what says the four longhands
  are read independently rather than one value being applied to all of them.
- **`#pill`** asks for 999px on a box 40px tall. CSS scales EVERY radius on the box by the same
  factor — the smallest that makes each side fit — rather than clamping each corner on its own.
  Scaling per side gives a rectangle with mismatched circular ends; scaling uniformly gives the
  pill, which is what a browser draws.

Radii are read from the four longhands rather than from the shorthand, because the cascade expands
the shorthand into them — which also means the shorthand's own syntax, up to eight values split by
a slash, never has to be parsed here.

One limit, and it is reported rather than silent: a radius is honoured on the background always,
and on a border only where the border is painted as one ring, which needs every edge solid and
every edge the same colour. Anything else falls back to four mitred trapezia, which have square
corners — so the fill underneath is rounded and the frame over it is not, and `UnsupportedCss` says
so.

What to look at: the corners of `#pill` and `#bordered`. A `#pill` with circular ends of different
sizes is the clamp applied per side rather than to the box.

**Boxes**: 7 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0005 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

