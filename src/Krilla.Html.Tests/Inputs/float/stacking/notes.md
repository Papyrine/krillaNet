Where a second float goes when a first is already there. Four arrangements, because the rule is not
one rule.

- `#fit` places two left floats side by side, the second starting where the first ends.
- `#drop` makes them 450px between them in a 400px container, so the second cannot fit beside the
  first. It descends to the first float's bottom edge and returns to the left edge — it does not
  shrink, and it does not overlap.
- `#rights` stacks two right floats, and the order is the one that surprises: the float written
  FIRST sits furthest right. Reading the source left to right gives the painted order right to
  left.
- `#both` puts one on each side and leaves the paragraph the band between them.

`#drop` is the case that decides whether the placement search is a search at all. Handing each float
the next free position along one axis gets `#fit` right and `#drop` wrong, and a single scenario
covering only `#fit` would never notice.
