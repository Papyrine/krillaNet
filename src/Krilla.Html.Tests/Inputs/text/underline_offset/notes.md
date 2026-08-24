# text/underline_offset

`text-decoration-thickness` and `text-underline-offset`, and the two measured rules underneath them
that had been wrong at every size but 16px without anything noticing.

The scenario is at **20px**, which is the point. `text/decorations` measures the default rules at
16px and is pixel-identical, and that agreement turned out to be a coincidence: the engine read the
face's `post` table, Chrome does not read the face at all, and the two round to the same answer at
16px and part company immediately above it.

Measured out of Chrome across nineteen sizes from 10px to 60px, exact at every one:

- **Thickness is `max(1, floor(size / 10))`.** Nothing to do with the font — Liberation Sans gives
  0.8px at 16px, and both rules give 1 there. At 20px the font gives 1 and Chrome draws 2. The FLOOR
  is the whole of it: 19px is one pixel thick and 20px is two, which no rounded expression
  reproduces.
- **The underline sits `ceil(size / 20)` below the baseline**, held clear by at least half its own
  thickness: `max(ceil(size / 20), floor(thickness / 2))`. Both halves are needed. The size term
  alone is a pixel short for a thick rule — `#thick` at 4px sits two pixels down where the size says
  one — and the thickness term alone is a pixel short at 44px. The CEILING is what puts the step at
  21px rather than at 20px, and the position is flat from 24px to 40px, where anything linear in the
  size would keep climbing.
- **A declared `text-underline-offset` REPLACES that position rather than adding to it.** CSS
  describes the property as an offset from the initial position, which reads as additive; at 20px the
  resolved position is one pixel down and `text-underline-offset: 6px` puts the rule six down rather
  than seven. `#lowered` is the row that says so and `#both` confirms it against an overridden
  thickness at the same time.

The line-through keeps the FONT's position — `OS/2.yStrikeoutPosition` — and takes the resolved
thickness like the other two. Only the position is a property of the face: a strike has to cross the
glyphs at the height that face was designed for, where a thickness is the browser's choice.

**Residual**: SSIM 0.9997 from glyph edges. Every one of the six rules lands on exactly the rows
Chrome puts it on.

What to look at: the rows each rule occupies. `#thin` is one row, `#plain` two, `#thick` four, and
`#lowered` starts six below the baseline rather than seven.
