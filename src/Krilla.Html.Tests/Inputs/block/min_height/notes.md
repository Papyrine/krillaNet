# block/min_height

`min-height` and `max-height` were read by nothing and reported by nothing, so a box carrying
either was laid out at its content height and the document was silently wrong below it. They now
resolve alongside the width pair they mirror, and this measures all three ways they can fail.

- **`#held`** — one line of content in a box with an 80px minimum. The simplest case, and the one
  that survives if the property is read and then dropped.
- **`#cut`** — four lines in a box with a 40px maximum. Nothing is clipped: `overflow` is not
  implemented and is reported, so the content runs past the bottom edge and over what follows,
  which is exactly what `overflow: visible` asks for. A box that swallowed its overflow would look
  tidier and be wrong. That the overflowing text stays visible over the next box is a separate
  property, and `block/overflow_paint` is where it is measured.
- **`#both`** — a minimum taller than the maximum. `ClampHeight` applies the maximum first, so the
  minimum wins at 90px, the same order `Clamp` uses horizontally.

Percentages are deliberately absent: a percentage height resolves against a containing height that
is indefinite throughout a paginated document, and CSS says such a percentage behaves as though it
were not there — the same rule `height` itself follows here.

What to look at when it moves: `#held` at one line tall is `min-height` unread. `#cut` at four
lines is `max-height` unread, and `#cut` with its text cut off at 40px is overflow being clipped
where nothing should clip it. `#both` at 30px is the two clamps in the wrong order.
