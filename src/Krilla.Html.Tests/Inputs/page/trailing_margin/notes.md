# page/trailing_margin

A document whose content stops short of the sheet under a box whose bottom margin reaches past it.
It printed on two pages here and on one in the browser, the second holding nothing at all.

The root element's margins never collapse (CSS 2.1 §8.3.1), so the bottom margin of whatever ended
the document is trapped INSIDE the root's box rather than escaping it. `#tail`'s 60px therefore
makes the root 1088px tall against a 1056px sheet, while nothing in the document reaches past 1028 —
and pagination was measuring the root's own edge.

It measures the deepest BOX now. A margin is the only thing that drops out: an empty box a thousand
pixels tall is content and takes the pages it asks for, which is why the walk is over boxes rather
than over ink, and why a declared height on the ROOT still counts — that being the one case where
the root's own box is the deepest thing rather than an artefact of what it contains.
`PaginationTests.ATrailingMarginAddsNoPage` keeps both halves, since a second page with nothing on
it is not something a browser reference can express.

**Residual**: SSIM 0.9996, sub-pixel glyph positioning on the two paragraphs.

What to look at: the PAGE COUNT, which is the whole of what this measures. A second page is the
margin being counted as content.
