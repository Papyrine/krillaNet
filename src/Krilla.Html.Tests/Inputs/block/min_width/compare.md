# block/min_width

# block/min_width

`min-width` is implemented, with an explicit precedence rule — applied after `max-width`, so a
minimum wider than the maximum wins, which is the order CSS specifies — and it appeared in no
scenario at all. `block/max_width` measures only the other half of the pair.

Three arrangements, because the property fails in three different ways:

- **`#held`** — a declared width below the minimum. The simplest case, and the one that would
  survive if `min-width` were read and then dropped.
- **`#both`** — a minimum wider than the maximum. This is the one the precedence rule exists for:
  clamping to the maximum last gives 100px, which looks correct until it is compared.
- **`#relative`** — a percentage, resolved against the containing block rather than the viewport,
  which is the distinction `StyleResolver` reads the cascaded style for.

What to look at when it moves: `#held` at 50px is `min-width` unread. `#both` at 100px is the two
clamps applied in the wrong order, and at 300px is neither applied. `#relative` at any width other
than 240px is a percentage resolved against the wrong box.

**Boxes**: 8 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0009 · SSIM 0.9998** |
| <img src="reference_0001.png" width="480"> | <img src="result%23page_0001.verified.png" width="480"> |

