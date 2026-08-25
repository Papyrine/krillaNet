# page/orphans_widows

Two paragraphs caught at a page boundary, one violating `orphans` and one violating `widows`, under
the initial counts of two and two.

This is the scenario the corpus did not have, and its absence is why a wrong note stood for so long:
`HtmlOptions.HonourOrphansAndWidows` was off by default on the stated grounds that **Chromium does
not implement the properties**, and nothing here could tell. Every scenario that broke inside a
paragraph happened to break somewhere both counts permit, so the constraint changed nothing whether
it was on or off.

It is not a fine distinction. With the constraint off this engine produces **two pages against the
browser's three** — the sharpest signal the corpus has, and it was there to be measured the whole
time.

## The two halves

- **`#orphan`** starts 1012px down, so only one of its four lines fits on the first page. That is
  one short of `orphans: 2`, and moving the break earlier only takes more lines off the top — so
  nothing but moving the whole paragraph fixes it, and Chromium moves the whole paragraph. Page one
  ends 44px short of the sheet.
- **`#widow`** straddles the second boundary with one line left below it, one short of `widows: 2`.
  Here the run IS long enough to give a line up: moving the break one line earlier leaves three
  above and two below, which satisfies both. Chromium moves the break rather than the paragraph.

The two are the whole rule between them. A test with only the first would pass an engine that moved
every constrained run whole and wasted a page each time; a test with only the second would pass one
that never moved a run at all.

## The case this deliberately does NOT contain

A run too short to satisfy both counts at once — which under the initial two and two means any
paragraph of three lines. `page/break_between_lines` holds that one, and Chromium splits it two and
one rather than moving it whole, leaving a widow the property forbids. Reproducing that was the
whole of the work here: this engine moved the run overleaf, which is the tidier answer and is not
what the browser does.
