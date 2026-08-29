# text/ligatures

Ligatures, where shaping changes the glyph count rather than only the spacing: fi and fl become a
single glyph, so the text measures narrower than its characters would suggest.

The cluster mapping matters here as much as the width. A ligature covers several characters with
one glyph, so its text range spans them all — get that wrong and the PDF's text extraction returns
the wrong characters for the run even though the page looks right.

**Residual**: SSIM 0.9982, the same cause `text/kerning` writes up in full — PDFium truncates the
fractional `/W` glyph widths krilla writes, so a run drifts left by up to a thousandth of an em per
glyph. This scenario shows it for the same reason that one does: its text is large, and the error
is proportional to the font size.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0099 · SSIM 0.9982** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

