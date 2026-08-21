# table/anonymous

A table lays out its children by ROLE rather than in order, so a child with no table role is not
merely misplaced — without intervention it is never positioned or painted at all.
`BoxBuilder.TableFixup` wraps such children in the anonymous rows and cells CSS requires, and its
geometry had never been measured against a browser: the comment saying so is in CLAUDE.md, and
this scenario is what removes it.

Unreachable from ordinary HTML, whose parser moves stray content out of a `<table>` before the
cascade ever sees it. Reachable from `display: table` in a stylesheet, which is what this uses.

Three children, each needing a different amount of invention: a cell with no row around it, a
properly formed row, and a block with no table role at all. The first two are the common shapes; the
third is the one where content disappears entirely if the fixup is missing.

What to look at when it moves: content on the page at all is the first thing this measures. After
that, each anonymous row occupies a row of its own — the loose cell, the two-cell row, and the
loose block stacked in source order, never merged into one row.
