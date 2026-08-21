# table/empty

# table/empty

An empty table occupies nothing — not two pixels square, which is what the edge border spacing
would give it if the spacing were added unconditionally. With no columns there is nothing for that
spacing to be outside of.

The paragraphs are the instrument. Under `flatten.css` they carry no margins, so they stack
tightly and any occupancy at all by a table between them shows up as a shift of everything below
it. Four paragraphs and three degenerate tables mean each table's contribution is isolated.

`#spaced` is the arrangement that distinguishes the two readings: a table with `border-spacing: 20px`
and no cells is 0x0, not 40x40.

All three occupy nothing, and the page was pixel-identical to the reference from the start. One
box was not: `#rowless`'s `<tbody>` sat at x=2, the default 2px edge spacing applied to a section
with no columns to be outside of, where the table's own width had known to skip it all along.
`PlaceRowBoxes` now applies the same rule. It was invisible — an empty section paints nothing — so
only the geometry comparison could ever have seen it, which is the case for that comparison
leading.

What to look at when it moves: any gap between two paragraphs. A 4px one is the default 2px edge
spacing being added twice; a 40px one is `#spaced`'s own declaration being added the same way. The
`tbody` back at x=2 is the section's edge spacing returning.

**Boxes**: 10 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

