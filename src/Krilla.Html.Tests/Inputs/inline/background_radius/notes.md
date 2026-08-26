# inline/background_radius

`border-radius` on an inline element, which was read and not honoured — so every `<code>` chip in
every document came out with square corners, and the diagnostic said so rather than the painter
drawing it. The scenario measures the case that made it worth doing: a code span with a background
and a radius is the most common rounded inline there is.

## Why it is not one rectangle

An inline element's background is painted **per line fragment**, and a fragment is not one
rectangle. It is the opening edge's strip, then each run's own fill, then the closing edge's strip,
each painted separately and abutting. Rounding those individually puts corners in the **middle** of
the element, where two pieces meet. So the pieces are unioned per line and the fill goes down once.

The union is taken from the SNAPPED pieces rather than snapped after the fact. That is what keeps
every other scenario byte-identical: an element with no radius is still painted by exactly the
arithmetic it was before.

## Which ends are rounded

Only the ends the element itself reaches. `#wrapped` is the row that shows it: the highlight breaks
across three lines, and the middle fragment is square at **both** ends because the element neither
began nor finished there. A browser does the same, which is what makes a wrapped highlight read as
one continuous run of colour rather than three separate pills.

The obvious signal for this is the element's edge tokens, which carry `Leading` and `Trailing`. It
is the wrong one: those are emitted only when the element has a padding or border to put in one
(`BoxBuilder.HasSurround`), so they answer for `#chip` and are absent for `#bare`. Asking them
squares every unpadded highlight at both ends — which is what the first version did, and what the
`#bare` and `#wrapped` rows caught. Which lines the element occupies answers for both, so that is
what `InlineSpans` records.

## The rows

| | |
|---|---|
| `#chip` | Padding and a radius: the ordinary code span, rounded at both ends |
| `#bare` | A radius with **no padding**, so there is no edge strip to carry the corner |
| `#wrapped` | Three fragments — rounded, square, square, rounded — around two line breaks |
| `#nested` | A rounded element inside another, each filled at its own height |
| `#pill` | `999px`, which CSS clamps by scaling every radius until each side fits |

## What is still reported

A radius on an inline element that also has a **border**. The fill beneath one is rounded now, but
the border itself is still up to four rectangles — a fragment has no corners to mitre at the end
where the line broke — so those corners stay square and `UnsupportedCss.InlineSurround` reports
them. That is why the report narrowed to `HasBorder` rather than disappearing.

## Residual

SSIM 1.0000, AE 0.0004. Every differing pixel is sub-pixel glyph antialiasing at a line start, the
same residual several `text/` and `inline/` scenarios carry; none of it is on a corner arc.
