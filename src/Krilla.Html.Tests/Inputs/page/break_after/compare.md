# page/break_after

# page/break_after

`page-break-after` on the box BEFORE the break, where `page/break_before` declares it on the box
after. The two are the same page in the end, and the scenario exists because getting there is not
the same calculation.

`#one` carries a 40px bottom margin, which is what makes the scenario discriminating: its border
box ends at 48 and `#two` begins at 88, so "break after this box" has two plausible readings that
are 40px apart. Measured out of Chrome before anything was implemented — it starts page two at
`#two`'s top and the margin is gone entirely, never drawn at the foot of page one and never drawn at
the head of page two.

Breaking at the declaring box's own bottom edge instead is the mistake this catches, and it is the
more obvious of the two readings to write: every box on page two lands 40px low, beneath a band of
margin at the top of a page that should open with content. `Paginator.ForcedBreaks` resolves a
`break-after` to the top of the next in-flow box in document order for this reason, which makes
both properties the same thing to the slice — a page starts at some box's top border edge.

A margin is the only thing that separates the two readings, so a version of this scenario without
one would pass against either implementation.

Both pages are pixel-identical to Chrome.

What to look at: whether `#two` sits flush at the top of page two, and whether any part of the
margin appears at the foot of page one.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |
| **Page 2** | **Page 2. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0002.png" width="480"> | <img src="result%23page_0002.verified.png" width="480"> |

