# text/text_transform

Casing, and where a word starts.

The first three rows are the straightforward half. Each box is an inline-block so its width IS the transformed
advance, which makes the reference geometry a direct measurement of the cased text rather than of
the paragraph it sits in — `hello world` upper-cased is 116.47px against 76.48px, so a transform
that silently did nothing would show up as a 40px box difference rather than only in the pixels.

`#boundaries` is the row that had to be measured. `page-break o'clock 3rd (bracketed) "quoted"`
comes back from Chrome as:

> Page-Break O'clock 3rd (Bracketed) "Quoted"

So a hyphen, a bracket and a quote each begin a word, and an apostrophe and a digit do not. The
obvious rule — a word starts after any non-letter — gives `O'Clock` and `3Rd`, which is wrong in
two places out of five. The rule that reproduces every case is per PRECEDING character: a letter
starts a word unless what comes before it is a letter, a digit, or an apostrophe. That is an
approximation of the UAX #29 word segmentation a browser runs, where the apostrophe is MidLetter
and a digit joins the letters around it — the same answer arrived at properly.

`#none` is here to be identical to `#lower`'s output, which is the check that the property is read
at all rather than that upper-casing happens to be the default.

The transform is applied after white-space processing and before shaping, which is the order that
matters: a collapsed run of spaces has already become one space when word boundaries are looked
for, and the text that is shaped, broken into lines and painted is one string throughout. It cannot
be a painting concern — upper-casing makes a line half again as wide in most faces and wraps a
paragraph a line earlier.

One limit: the boundary is found within a single text node, so a word split across two inline
elements is capitalised at the start of the second where a browser looks at the rendered text whole.
Rarer than the same limit in line breaking, since capitalising mid-word is not something markup
normally sets up.

Geometry is exact and pixels read SSIM 0.9999.

What to look at: `#boundaries`. `O'Clock` or `3Rd` is the naive boundary rule returning.
