# table/cell_baseline

`vertical-align: baseline` on a table cell, which lines a row's cells up on their first baselines
rather than on any edge of the row. It was rendered as `top` and reported as unimplemented.

It is the property's initial value and still not the usual case: the user-agent stylesheet gives a
table `middle` and its cells `inherit`, so a cell only reaches `baseline` by asking for it. That is
why it earned a report rather than being a silent default, and why implementing it moves no other
scenario in the corpus.

Three tables, and the third is what keeps the other two honest:

- **`#mixed`** — 16px text, 32px text, and a cell with 18px of extra top padding. All three land
  their first baseline on the same line, 33px below the row's top edge. The 16px cell's content is
  moved down 16px to get there and the other two do not move at all, which is the point: the row's
  baseline is the FURTHEST any cell carries its own, so most cells sit still and one does the
  travelling.
- **`#lines`** — a one-line cell, a two-line cell and a 32px cell. The two-line cell aligns on its
  FIRST line, not its last, so its second line hangs below the row's baseline and makes the row
  60px tall where the same three cells top-aligned need 44. A row's height is therefore not the
  tallest cell: it is the furthest above the baseline plus the furthest below it, and those two
  maxima come from different cells.
- **`#against`** — `top`, `bottom` and `middle` beside a baseline cell in one row. None of them
  takes part in the baseline, and the row is sized by the tallest of them instead. Without this row
  an implementation that treated every cell as baseline-aligned would pass.

**Geometry is exact on all 33 boxes**, which is what says the rule is right — every span in every
cell lands where Chrome's layout puts it.

**Residual**: SSIM 0.9926, and it is not this engine's. **Chromium's PRINTER does not apply the
alignment, though it reserves the room for it.** The two halves of this scenario's reference
disagree: `getBoundingClientRect()` puts `#lines`'s `.one` span at y=87, and the printed page draws
that same text at y=71 — top-aligned, 16px higher — while both agree the row is 60px tall. So the
browser computes the taller row that baseline alignment demands and then leaves the content against
the top of it.

That is the same shape as `visibility: collapse` on a table row, where Chrome also disagrees with
itself and no engine behaviour can be exact on both measurements. It is resolved the other way here,
because the disagreement is one-sided rather than symmetric: the box geometry agrees with CSS 2.1
§17.5.4 and with every other browser, and the print render agrees with nothing — including its own
row height. Matching the geometry is matching the specification; matching the pixels would mean
reproducing a bug that the browser's own layout contradicts.

What to look at: the box comparison, which is the measurement that means something here. Any
non-zero offset is a real regression. The page render will differ on every baseline-aligned cell
whose content had to move, and only on those — `#against` is pixel-clean.
