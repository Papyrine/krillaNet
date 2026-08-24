# block/overflow_hidden

# block/overflow_hidden

Clipping, measured as a pair. `#clipped` and `#visible` hold the same 96px of content in the same
48px box, and differ only in the property — so the scenario reports the effect of `overflow` rather
than the effect of a short box, and a version with only the clipped half would pass against an
engine that had lost the overflowing text.

`#wide-parent` is the horizontal case, one unbreakable line in a 200px box. Worth having on its own
because the vertical clip could be produced by accident: a paginator that stopped at the box bottom
would clip downward and not sideways.

The clip is to the PADDING box (CSS 2.1 §11.1.1), not the content box and not the border box — so a
box with padding shows its content inside that padding and cuts at the inner edge of its border.
None of the boxes here has a border, which the scenario is honest about: what it measures is that
clipping happens and where the bottom edge falls, not which of the three rectangles was used.

The implementation is the part worth knowing. The clip is pushed inside each of Appendix E's
PHASES, for the duration of that phase's walk over the box's subtree, rather than once around the
box. Painting an `overflow` box's subtree as one unit under a single clip is what a stacking context
would do, and this is not one: it would put the box's text down during the background phase, where
a later sibling's background could cover it — the defect `block/overflow_paint` exists to catch.
Each phase visits a subtree as one contiguous stretch, so a clip held across that stretch covers
exactly the right boxes and the global phase order survives.

Geometry is exact and pixels read SSIM 1.0000.

What to look at: the boundary between the third and fourth line of `#clipped`. Four lines means the
clip is not being pushed; two lines in `#visible` means it is being pushed for every box.

**Boxes**: 8 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0001 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

