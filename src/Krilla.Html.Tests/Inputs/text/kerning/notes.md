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

The error is proportional to the FONT SIZE, which is why this scenario and `text/ligatures` show it
and the 16px scenarios do not — measured, a 16px line drifts by 0.000px end to end. So this
explains the two worst text residuals in the corpus and none of the others, whatever the rest of
them turn out to be.
