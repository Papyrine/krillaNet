# image/object_fit

# image/object_fit

Pixel-identical to Chrome. Four values, one image, one box — and the box is the same 160x60 in all
four rows, which is the point: `object-fit` changes what is drawn INSIDE a replaced element's box
and never the box itself. The geometry comparison confirms that for free.

The swatch is 64x32 and the box is 160x60, deliberately a different aspect ratio, because the four
values are indistinguishable when the two agree. Measured against Chrome:

- **`fill`** stretches to the whole 160x60, ignoring the image's proportions. It is what this
  engine already did, so it is here as the control.
- **`contain`** scales to fit inside: 120x60, centred, leaving 20px of frame either side.
- **`cover`** scales to cover: 160x80, clipped to the box's height.
- **`none`** draws at the intrinsic 64x32, centred, at x=48 and y=14.

The centring is `object-position`'s initial value and is not separately implemented — a document
naming a different position gets the centre, which the diagnostic table does not yet cover.

A clip is pushed only where the content can reach outside the box, which is `cover` and an
oversized `none`. Every clip is a graphics state push in the PDF, and `fill` and `contain` never
need one.

The frame carries a background so the letterboxing `contain` leaves reads as itself rather than as
blank paper — without it, a `contain` that silently fell back to `fill` and one that worked would
both look like a picture on white.

What to look at: the left and right edges of `#contain` and the top and bottom of `#cover`. Ink
reaching the frame's edge in `#contain` is a fallback to `fill`; ink outside the box in `#cover` is
the missing clip.

**Boxes**: 10 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

