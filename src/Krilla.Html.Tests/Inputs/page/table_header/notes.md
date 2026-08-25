# page/table_header

A twenty-two row ledger over three sheets, with a `<thead>`.

A browser's printer re-draws the header group at the top of every page the table continues onto and
moves the rest of the table down to make room, which is the one thing people expect from
HTML-to-PDF conversion of a long table: without it, page two of a report is a grid of unlabelled
columns. Chromium does it here, and so does this engine.

Like `page/fixed_repeat`, the repetition is a PIXEL measurement and only that. The geometry harvest
runs against one continuous layout, so the browser reports the `thead` exactly once — an engine
that drew it on page one alone would still match all 99 boxes.

## What each part of the arrangement is for

- **The 96px left margin.** A repeated header keeps the TABLE's left edge, not the page's. The two
  coincide at zero and nowhere else, so an indented table is what tells them apart.
- **The tall intro paragraph.** It pushes the table's start well down page one, so the first repeat
  is at a different place on the sheet from the original rather than at the same one.
- **`border-collapse: collapse` with a filled header row.** The two together found a defect on the
  first render, below.
- **The long closing paragraph.** Page three begins past the table's bottom edge and carries no
  heading row at all, which is the other half of the rule: a header repeats while there is still a
  row of its table to label and stops the moment there is not. That page is pixel-identical.
- **Its length in particular.** The break inside it leaves several lines either side. An earlier
  version left ONE line on page three, and Chromium moved a second down to join it — its `widows`
  handling, which this engine has switched off by default. The scenario is not asking that
  question, so it stays clear of it.

## The defect it found

A collapsed table's grid lines were painted with the TABLE's own decoration, which in tree order is
before every one of its cells' backgrounds — and a cell's background reaches the middle of the line,
because under the collapsing model a cell's border box includes half of every rule around it. So the
rule between the header row and the first body row was painted and then covered, and the header
came out with no line under it.

Invisible until now because no collapsed table in the corpus had a cell background: `table/collapse`
measures six arrangements of rules and fills none of its cells. CSS 2.1 Appendix E says the
collapsed borders go down after the backgrounds of all the table's elements, so the fix is one move
— out of `Decorate` and into the end of the `Backgrounds` walk — and it left every other scenario
identical.

## The band, and where it starts

The strip re-drawn at the top of a continuation page starts at the TABLE's top edge rather than at
the header's. Measured: Chromium lands the first continued row 61.5px down a page whose header
group is 61px tall, and the half pixel is the top rule the table draws above it. Reserving the
header's own height instead puts every horizontal rule on the page one device pixel high, which is
what page two looked like before it was measured — 0.9185, against 0.9934 after.

## Residuals

Sub-pixel glyph positioning, at 24 and 30px, and nothing else: no pixel on any of the three pages
differs by more than 81 of 255, so no glyph is in a different place, only antialiased differently at
its edges. Page three has none at all.
