# text/font_size_keywords

# text/font_size_keywords

The seven absolute keywords, the two relative ones, and a relative one nested inside an absolute
one. Every box is an inline-block so its width is the text's advance, which turns a resolved font
size into a number the reference geometry reports directly — a keyword that quietly fell back to
the inherited size would show as an identical width rather than as a subtly different render.

The absolute table was measured rather than derived, because it is not a series any ratio produces:
9, 10, 13, 16, 18, 24 and 32 pixels. The steps at the small end are one pixel and the steps at the
large end are eight, so any single scaling factor is wrong at one end or the other.

It is anchored on a CONSTANT rather than on the root element's size. CSS says the table follows the
reader's preferred font size, which is a setting this engine has no equivalent of and which a
browser does not take from the document — so `font-size: large` is 18px whatever `html` declares.
Anchoring on the root instead would make a document setting `html { font-size: 20px }` report
`large` as 22.5px where Chrome still says 18.

The two relative keywords are the parent's size scaled by 1.2, measured and holding at every size:
16px gives 13.333 and 19.2, and `#nested` — `smaller` inside a `large` parent — gives 15, which is
18 over 1.2. That last row is what says the keyword resolves against the INHERITED size rather than
against the root's.

There is a specific failure behind this scenario. The keyword used to reach `ResolveFontSize` as an
unparseable value whose fallback was an absolute zero, so `font-size: large` resolved to a size of
0 and deleted the text of the element carrying it. `DiagnosticTests` keeps a regression test for
that, and now also pins the table above.

Geometry is exact and pixels read SSIM 0.9997, from glyph positioning across nine different sizes.

What to look at: the widths. Any two rows coming out equal means a keyword fell through to the
inherited size.

**Boxes**: 13 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0006 · SSIM 0.9999** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

