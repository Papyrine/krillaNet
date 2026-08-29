# flex/column

# flex/column

A column container is the same algorithm with the axes exchanged, and it is where an implementation
that special-cased rows falls over. Four columns side by side, so each one's vertical arrangement is
legible against the others. Geometry-exact on all 19 boxes and pixel-identical to Chrome.

- **`#auto`** is the case a column implementation is most likely to get right by accident, and the
  one the other three are measured against. A column container with an auto height has an
  INDEFINITE main size, so nothing grows, nothing shrinks and nothing wraps: the items stack at
  their content heights and the container comes to their sum.
- **`#fixed`** is the same container given a definite height, at which point the main axis becomes
  flexible exactly as a row's width always is. 200 of room less a fixed 30 leaves 170 to share one
  to two.
- **`#spread`** runs `justify-content` DOWN the column, so `space-between` puts the first item at
  the top and the last flush with the foot.
- **`#across`** is the axis exchange itself: the cross axis of a column container is horizontal, so
  `align-items` decides WIDTH here. Which is what makes `stretch` the reason a column's children
  fill it, and why `#w2` and `#w3` shrink to their own content instead.

The scenario does NOT catch the zero-width bug `flex/direction` found, and that is worth recording:
every item here is sized by its content, so the cross size is settled on the branch that computes a
natural height. An item with a DECLARED height skips that branch, and the width it never received
was zero.

**Boxes**: 19 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

