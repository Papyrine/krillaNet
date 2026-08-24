# text/soft_hyphen

# text/soft_hyphen

Pixel-identical to Chrome, and geometry-exact.

A soft hyphen is a break opportunity that draws a hyphen only where the break falls on it, which
makes it the one character in the corpus whose rendering depends on a decision taken after it was
measured. Four rows separate the parts:

- `#narrow` at 40px takes both opportunities and draws a hyphen at each, over three lines.
- `#wide` at 300px takes neither, and measures **87.19px, exactly what `#none` measures without any
  soft hyphen in it at all**. That equality is the whole property and it is what forced the
  implementation: the soft hyphens are stripped BEFORE shaping, because this face maps U+00AD onto
  a real hyphen glyph with a real advance. Left in the string, the word would measure wider than
  the same word without and would draw hyphens no break called for.
- `#late` at 70px passes over the first opportunity and takes the second, which is what a greedy
  line breaker does and what distinguishes a real implementation from one that always breaks at the
  first.
- `#none` is the same word with nothing in it, overflowing a box it cannot break in.

The drawn hyphen belongs to the ELEMENT that broke, which was measured rather than assumed: Chrome
reports the span 5.33px wider than the text inside it, exactly the hyphen advance. So the hyphen run
carries the token selector and inline ancestry, and an inline background reaches under it.

Stripping happens at tokenisation over one shaped run, the same arrangement dash breaking uses, so
the segments sum to exactly what the whole word measured and the kerning across the join survives.

**Known limitation.** The hyphen width is not part of the fit test, only of the line it ends. A line
whose last segment leaves less than a hyphen of slack therefore overruns its box by up to that much,
where a browser would move the segment down. None of the four rows here is in that condition, and
correcting it needs the line breaker to back up over a decision it has already taken.

What to look at: the hyphens at the ends of the first two lines of `#narrow`, and the absence of one
anywhere in `#wide`.

**Boxes**: 10 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

