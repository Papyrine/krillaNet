# page/tall_image

A picture too tall for any sheet, in the middle of a LINE rather than on a block of its own. It
cannot be made to fit, so where it goes is a choice between two wrong answers, and the two are far
apart.

The block-level case was already right: `Paginator.Unbreakable` lists a replaced element alongside a
table row, so a block image moves whole. An inline one is not a `LayoutBox` at all — it hangs off
the line — so it reached pagination as a LINE, and a line taller than the page was stepped over on
the reasoning that moving it would leave it still not fitting and it would move again forever.

That reasoning is half right. A unit already at the page's top has nowhere better to go and must be
stepped over; one starting BELOW the top can be moved, and moving it advances the loop, so it
terminates. Chromium does exactly that: the picture starts on a fresh sheet and overflows onto the
next, leaving page one holding the paragraph above it. Sliced where the page happened to end it
would have drawn its top on page one and its middle on page two instead.

Three sheets: the intro alone, then 1056px of picture, then its last 144px. The first two are
pixel-identical.

**Residual**: page three, where the paragraph after the picture sits 16px lower here than in the
reference — exactly the paragraph's own top margin, which the printed page drops and the browser's
own `getBoundingClientRect()` keeps. The box comparison is exact on all six boxes, so this engine
agrees with Chromium's layout and differs from Chromium's printer, which is the same shape
`table/cell_baseline` records.

What to look at: the page COUNT and which sheet the picture starts on. A picture beginning on page
one is the unit being sliced; four pages is it being moved twice.
