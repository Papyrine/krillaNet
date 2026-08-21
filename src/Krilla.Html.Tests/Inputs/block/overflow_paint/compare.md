# block/overflow_paint

# block/overflow_paint

CSS 2.1 Appendix E is a set of phases over a whole stacking context, not an order within one box:
every in-flow block background and border goes down in tree order (step 3), then the floats (step
4), then all the inline content (step 7). Applying the same sequence box by box instead is
indistinguishable while nothing overlaps, and this is what it looks like when something does.

- **`#cut`** overflows its own box by three lines, and those lines land on `#under`. Painted per
  box, `#under`'s background arrives after them and hides them; painted in phases, the text is on
  top, which is what a browser does.
- **`#hang`** is a float taller than the box that declared it, and a block does not grow to
  contain its floats — so it hangs over `#below` entirely. Same defect, reached the other way: a
  float belongs to the layer rather than to the box that declared it, so it goes down after every
  background in the layer and before every line.

Nothing else in the corpus overlaps two boxes without positioning one of them, which is why the
whole restructure it measures is invisible everywhere else: all 69 other scenarios came out
pixel-identical across it, and only the order of the operators in their PDFs changed.

What to look at when it moves: either coloured block covering what should be over it. `#cut`'s
last three lines disappearing is the background phase losing its reach; `#hang` disappearing is the
float phase losing its.

**Boxes**: 8 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0003 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

