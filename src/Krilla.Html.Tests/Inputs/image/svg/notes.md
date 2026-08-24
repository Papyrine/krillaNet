The first vector image in the corpus. Six rows, and the middle three measure things a raster image
cannot be asked.

`#intrinsic` takes its size from the root element's `width` and `height` attributes, exactly as a
PNG takes its from the IHDR chunk. `#wide` gives a width alone and the height follows from the
ratio — the same sizing path `image/sized` measures, the point here being that the ratio came out
of the SVG's header rather than out of a decoded bitmap.

`#viewbox` and `#inline` are the rows with no raster equivalent, and they found the rule. Their
documents declare no `width` or `height` at all, only a `viewBox`, and SVG's own specification
defaults those attributes to `100%` rather than leaving them absent — so such an image has an
aspect ratio and NO intrinsic size, and the percentage resolves against the containing block.
Chrome makes `#viewbox` 816 wide, the full content width, and `#inline` 400, the width of the `p`
that holds it. Neither is the 40 its viewBox names, and neither is the 300 of the default object
size that CSS 2.1's rule for a replaced element with no intrinsic width would give. That it holds
for the inline row as well as the block one is what says this is a percentage resolving, rather
than a block box's `width: auto` filling its container.

`#fitted` is `object-fit: contain` over a box whose ratio differs from the image's, where a vector
image and a raster one have to behave identically: the property never changes the box, only what is
drawn inside it, so the grey background shows the letterboxing.

`#tall` is 800px inside a page whose content is 1056, placed 884 down, so it straddles a boundary.
It found a defect that had nothing to do with SVG: a block-level replaced element was not treated
as an unbreakable unit, so the page break landed inside the picture and its top was drawn on the
page before. Chrome moves it whole. No raster scenario could reach this — it needs an image close
to a page tall, and every other image in the corpus is a 64x32 swatch. `Paginator.Unbreakable` now
lists it alongside a table row and a `break-inside: avoid` box.

`#text` is the row that says SVG text draws at all. usvg resolves text while it PARSES and matches
families itself, so it cannot be handed the face `FontSet` would pick — `ImageStore` loads every
registered face into a database of its own and makes the fallback's family the default. Without
that the label is silently absent, which is the worst way for a chart to convert. It reads 0.9988
rather than 1.0000, the sub-pixel glyph positioning every text-bearing scenario in the corpus
carries; the flat-fill rows either side of it are exact.

One limit worth recording, not measured here. An INLINE image taller than a whole page is still
sliced at the page edge rather than moved to a fresh page the way Chrome moves it, because that
path goes through the line breaker rather than through `Paginator.Unbreakable`. `#tall` is
block-level, so it takes the fixed path.
