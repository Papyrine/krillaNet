# text/kerning

Kerning pairs, at a size that makes a fraction of an em visible. AV, LT, Wa and To are the classic
ones: the pair is drawn tighter than the two advances would place it.

This is the scenario the corpus could not have before. Text used to be measured by summing raw hmtx
advances, which ignores kerning entirely, so `reset.css` disabled it in the browser to keep the two
sides comparable. Shaping through krilla's own rustybuzz removed that concession, and the third
paragraph checks the consequence that actually matters: with the wrong widths, a line breaks in the
wrong place.

**Residual**: SSIM 0.9945, and the cause is NOT what this file said for a long time. It was
recorded as sub-pixel glyph positioning, suspected to be accumulated float error against Chrome's
1/64px `LayoutUnit`. Measured, it is neither sub-pixel nor ours.

The glyph positions the two PDFs ASK FOR are identical — computed out of both content streams, the
worst disagreement anywhere on the page is six millionths of a pixel. The outlines are identical
too, byte for byte, hinting instructions included. What differs is that Chromium's printer writes
an explicit `Td` before every glyph while krilla writes one `TJ` array per run and leans on the
font's own `/W` widths for the base advance — and **PDFium truncates a fractional `/W` width to an
integer**. Liberation Sans at 2048 units per em produces widths like 666.9922 and 943.84766, so
every glyph loses up to a thousandth of an em, and the loss accumulates: by the right-hand end of
the first line the text sits 1.09px left of where the browser puts it.

Proved rather than argued. Rewriting this scenario's `/W` array with the widths already FLOORED
renders byte-identically to the unmodified file — zero differing pixels — which is only possible if
PDFium was flooring them anyway. Rewriting it with rounded integers instead drops the page from
17,339 differing pixels to 2,239.

It is an upstream defect and there is nothing to do about it here: krilla writes the fractional
width (`text/cid.rs`) and computes its `TJ` kerning adjustments against the full-precision advance
(`content.rs`), so the two agree and the PDF is correct for any renderer that honours `/W`. The fix
is to write `/W` as integers AND derive the adjustments from those integers, so the corrections
absorb the rounding and the position is right whether the renderer truncates or not. krilla is
pinned at 0.8.2, which is the newest published version.

Why it shows HERE and not across the whole corpus is the useful half. PDFium truncates for both
producers, so this is not something krilla does and Chromium avoids. What differs is when Skia stops
relying on `/W`: for a plain paragraph Chromium writes bare `Tj` operators and leans on the widths
exactly as krilla does, so both truncate identically and the renders match — `ua/paragraphs` is that
case and is pixel-identical. Where the text is KERNED, Skia switches to an explicit `Td` before
every glyph and becomes immune, while krilla stays on one `TJ` array whose base advance still comes
from `/W`. Which is why the corpus shows this in the two scenarios written to exercise kerning and
ligatures and nowhere else.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0201 · SSIM 0.9945** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

