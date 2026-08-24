# block/background_image

# block/background_image

Pixel-identical to Chrome across all sixteen rows.

A raster `background-image` was reported and not painted, so every panel, watermark and texture in
a converted document came out as flat colour. The gradient machinery was already here; what was
missing was the image, and the five properties that decide where it goes.

The load goes through the same `ImageStore` an `<img src>` goes through, which is the point rather
than a convenience: a `url()` in a stylesheet is bound by `LocalImages` and `WebImages` exactly as
one in the markup is. It would be a poor place to leave a hole, a stylesheet being the part of a
document a reader is least likely to have read. Two elements naming one file also share a decode.

Two rectangles are in play and they are not the same one:

- The **positioning area** is the PADDING box by default (`background-origin`), and
- the **painting area** is the BORDER box (`background-clip`).

`#padded` is the row that separates them: its first tile starts inside the border, and the strip
under the border carries the tail of the previous tile rather than the head of the first. That is
the same asymmetry `block/gradients` records, reached from the other direction — and it is why a
repeated background has to be backed up to the last tile that still reaches into the painted area
rather than started at the positioned one.

The four rows at the end pin the two properties independently, all with the same border and padding
so a difference between them is attributable:

- `#cliptopadding` fills only the padding box, `#cliptocontent` only the content box — and the
  latter also **clips the image**, whose position still came from the padding box, so it appears cut
  along the content edge rather than moved.
- `#fromborder` positions the first tile at the border box's own corner, where the border then
  covers the first eight pixels of it. `#fromcontent` positions it at the content corner while the
  colour still fills the whole border box.

`background-position` was the one measured rule worth having. A percentage does NOT offset by a
fraction of the box: it aligns that fraction of the IMAGE with the same fraction of the box, so
`25%` on a 64px image in a 200px box lands at 34px rather than at 50. `center` is exactly `50%` by
the same rule, and `right` exactly `100%`, which is what puts an image's far edge on the box's far
edge rather than off it.

`#proportional` is also what found the snapping. `75%` of the 12px left over is 9 exactly here, but
at the original 70px height it was 28.5 — and Chrome starts the tile on whole row 29 rather than
straddling two rows at half coverage. Rounding the origin was the last pixel between 0.9991 and
1.0000, and it is the same construction argument the inline background fill records: match how the
browser builds the shape, not how the specification describes it.

`#layered` states the thing most often got backwards: the colour and the image are two layers
of one background rather than alternatives, so the colour goes down first and a translucent image
shows it through.

The tile count is bounded at 512 per axis. A `background-size` resolving to a fraction of a pixel
would otherwise ask for hundreds of thousands of draws, and a page that takes a minute to write is
a worse answer than one whose pathological background stops early.

What to look at: the left edge of `#proportional` and `#centred`, and whether `#cliptocontent`'s
image looks cut or moved. Moved means the origin is being clipped along with the paint.

**Boxes**: 18 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

