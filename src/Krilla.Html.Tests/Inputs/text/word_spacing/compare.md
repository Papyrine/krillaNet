# text/word_spacing

# text/word_spacing

The companion to `text/letter_spacing`, and a simpler rule: the extra advance goes on each SPACE,
not on each word. `two words here` is three words and two spaces, and at 10px it comes back 20px
wider — a per-word reading would give 30px.

`#single` is the row that makes that unambiguous. One word, no space, and `word-spacing: 10px`
declared: the box must not change width at all. Without it, a per-word implementation and a
per-space one differ by exactly one increment everywhere and both look plausible.

`#trailing` declares `word-spacing: 0`, which has to be indistinguishable from not declaring it —
the check that a declared zero is not being treated as "unset" and inherited from somewhere.

`#tight` is negative, which pulls the words together rather than clamping.

Both spacings are applied inside `ShapedText` rather than by its callers, because they change the
answer to every question it exists to answer. A width that ignored them would break lines in the
wrong places and size a shrink-wrapped box wrongly, and adding them afterwards at some call sites
and not others is the shape that bug would take.

Geometry is exact and pixels read SSIM 0.9998, from glyph positioning across the widened spaces.

What to look at: `#single`. Any width change there is a per-word implementation.

**Boxes**: 7 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0009 · SSIM 0.9998** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

