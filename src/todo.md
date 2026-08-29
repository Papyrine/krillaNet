# Krilla.Html todo

What is still missing or wrong in the HTML converter, measured against Chrome by the corpus in
`src/Krilla.Html.Tests/Inputs`. Nothing here describes work that has landed.

**A finding is deleted the moment it lands**, and anything durable it taught moves to `CLAUDE.md`
— the traps sections exist for exactly that. This file is a working document and is expected to
shrink; for how something was fixed, read `CLAUDE.md` and the git history rather than looking for
a closed entry here.

Every claim below is either a number the corpus currently records or a construct the corpus
demonstrably does not cover. Where a scenario measures a gap, it is named. Where nothing measures
it, that is stated, because an unmeasured gap is the more dangerous kind.

## Where the corpus stands

162 scenarios across 11 categories, 2203 element boxes matched. **Box geometry matches Chrome
exactly on every one** — worst offset 0.00px, worst size 0.00px, and nothing unmatched — and 112
read SSIM 1.0000, of which 79 are pixel-identical outright. The other 33 differ on a scattering of
antialiased pixels, which is what `AE` is there to show.

Fifty read below 1.0000, and none should be "fixed" by regenerating a baseline. They used to be
described as having no mysteries among them; that is no longer true, and the change is a result
rather than a regression. Every row that said "sub-pixel glyph positioning" was repeating a cause
nothing had measured. Measuring it refuted it, replaced it for the scenarios carrying text above
16px, and left the rest honestly unattributed:

| Scenario | AE | SSIM | Cause |
| --- | --- | --- | --- |
| `page/tall_image` | 0.0122 | 0.9750 | Chromium's PRINTER drops a margin its own layout keeps, below |
| `block/background_repeat` | 0.0167 | 0.9842 | Chromium's PRINTER blurs a SPACED background's tile edges, below |
| `page/table_header` | 0.0049 | 0.9900 | Width truncation, below; drifts 0.28px at 24 and 30px |
| `table/cell_baseline` | 0.0031 | 0.9926 | Chromium's PRINTER does not apply cell baseline alignment, below |
| `text/kerning` | 0.0201 | 0.9945 | PDFium truncates krilla's fractional `/W` widths, below |
| `text/ligatures` | 0.0099 | 0.9982 | The same width truncation; drifts 0.36px |
| `page/table_footer` | 0.0114 | 0.9984 | Width truncation, below; drifts 0.22px |
| `block/shadows` | 0.0057 | 0.9985 | Antialiasing on `#rounded`'s corner; no pixel differs by more than 2 of 255 |
| `image/svg` | 0.0028 | 0.9988 | UNATTRIBUTED — no line drift, cause not yet measured |
| `block/border_radius_sides` | 0.0019 | 0.9988 | A corner seam where two colours hand over, below |
| `block/bevelled_borders` | 0.0009 | 0.9990 | Antialiasing where two colours meet on a mitre, below |
| `table/bevelled_borders` | 0.0005 | 0.9990 | The same mitres |
| `page/break_avoid` | 0.0045 | 0.9991 | Width truncation, below; drifts 0.39px |
| `page/fixed_repeat` | 0.0039 | 0.9992 | Width truncation, below; drifts 0.14px |
| `flex/min_size` | 0.0012 | 0.9994 | UNATTRIBUTED — no line drift, cause not yet measured |
| `block/border_styles` | 0.0002 | 0.9995 | A vertical dotted edge, below |
| `flex/basic` | 0.0000 | 0.9996 | Forty pixels in one column: a box edge at x=352.5, below |
| `page/break_between_lines` | 0.0017 | 0.9996 | Width truncation, below; drifts 0.15px |
| `page/trailing_margin` | 0.0024 | 0.9996 | Width truncation, below; drifts 0.10px |
| `block/counters` | 0.0013 | 0.9997 | UNATTRIBUTED — no line drift, cause not yet measured |
| `page/float_break` | 0.0023 | 0.9997 | UNATTRIBUTED — no line drift, cause not yet measured |
| `text/underline_offset` | 0.0002 | 0.9997 | Glyph edges |
| `block/min_width` | 0.0009 | 0.9998 | UNATTRIBUTED — no line drift, cause not yet measured |
| `page/break_inside` | 0.0012 | 0.9998 | Same, on page two; page one is identical |
| `page/multi_page_flow` | 0.0016 | 0.9998 | Width truncation, below; drifts 0.12px |
| `page/orphans_widows` | 0.0019 | 0.9998 | Width truncation, below; drifts 0.35px |
| `text/align_last` | 0.0013 | 0.9998 | UNATTRIBUTED — no line drift, cause not yet measured |
| `text/word_spacing` | 0.0009 | 0.9998 | Same, across the widened spaces |
| `block/box_sizing` | 0.0004 | 0.9999 | Same, on the one line beside a float |
| `block/list_image` | 0.0006 | 0.9999 | UNATTRIBUTED — no line drift, cause not yet measured |
| `block/outline` | 0.0006 | 0.9999 | UNATTRIBUTED — no line drift, cause not yet measured |
| `float/overflow_bfc` | 0.0005 | 0.9999 | UNATTRIBUTED — no line drift, cause not yet measured |
| `image/inline_flow` | 0.0006 | 0.9999 | One antialiased pixel column at an image edge |
| `inline/gradient` | 0.0111 | 0.9999 | Ramp quantisation, plus glyph edges |
| `inline/nowrap` | 0.0010 | 0.9999 | UNATTRIBUTED — no line drift, cause not yet measured |
| `inline/text_indent` | 0.0005 | 0.9999 | UNATTRIBUTED — no line drift, cause not yet measured |
| `link/fragment` | 0.0001 | 0.9999 | UNATTRIBUTED — no line drift, cause not yet measured |
| `link/wrapped` | 0.0000 | 0.9999 | UNATTRIBUTED — no line drift, cause not yet measured |
| `page/flex_break` | 0.0004 | 0.9999 | UNATTRIBUTED — no line drift, cause not yet measured |
| `page/tall_block` | 0.0004 | 0.9999 | UNATTRIBUTED — no line drift, cause not yet measured |
| `position/absolute` | 0.0005 | 0.9999 | One pixel column where two backgrounds meet at a fractional edge |
| `text/decoration_style` | 0.0002 | 0.9999 | `text-decoration-skip-ink`, deliberate, below |
| `text/font_size_keywords` | 0.0006 | 0.9999 | UNATTRIBUTED — no line drift, cause not yet measured |
| `text/letter_spacing` | 0.0005 | 0.9999 | UNATTRIBUTED — no line drift, cause not yet measured |
| `text/text_transform` | 0.0002 | 0.9999 | UNATTRIBUTED — no line drift, cause not yet measured |
| `text/word_break_opportunity` | 0.0002 | 0.9999 | Width truncation, below; drifts 0.18px |
| `ua/blockquote_pre` | 0.0005 | 0.9999 | UNATTRIBUTED — no line drift, cause not yet measured |
| `ua/presentational` | 0.0016 | 0.9999 | Corner mitres, plus width truncation at 0.13px |
| `ua/presentational_text` | 0.0006 | 0.9999 | Bevelled corners, two antialiased bullets, plus width truncation |
| `block/gradients` | 0.0500 | 0.9999 | Quantisation along the ramp; no pixel differs by more than 2 of 255, and the `AE` means nothing here |

**Four of these are the BROWSER's rather than this engine's**, and each is recorded in its
scenario's notes. `table/cell_baseline`: Chromium's printer reserves the taller row that cell
baseline alignment demands and then leaves the content against the top of it, disagreeing with the
same browser's own `getBoundingClientRect()` by exactly the offset. `page/tall_image`: its printer
drops the margin above the paragraph after an overflowing picture, which the same layout keeps.
`block/background_repeat`: it draws a SPACED background through a filtered shader, so every tile
edge in the reference is smeared across two pixels where ours is crisp — measured with a probe, a
box fitting three tiles with no gap renders crisply and the same box with a 4px gap at integer
positions does not. And `block/translucent`'s high `AE`, which is a one-unit rounding difference in
alpha compositing over large flat areas. In all four the box comparison is exact, which is what says
the disagreement is with the printer rather than with the layout.

Four of the rest are property gaps with a fix behind them — a vertical dotted edge,
`text-decoration-skip-ink`, the gradient quantisation and a rounded corner where two colours hand
over — and each is written up in the sections below. Two more are `text/kerning` and
`text/ligatures`, which are PDFium truncating krilla's fractional `/W` widths, measured and written
up under **Text**. Two are general: **a box edge landing on a fractional position**, and **two
antialiased edges meeting on a mitre**, which is the same shortfall `PaintUniformBorder` avoids for
a uniform border by painting one ring.

**Everything else in the table is now UNATTRIBUTED.** Those rows said "sub-pixel glyph positioning"
and that cause has been measured and refuted; nothing has yet measured what they really are. Each
is small — most differ on a few hundred pixels out of 861,696 — but "the same glyph positioning seen
on fewer words" is no longer an answer, and the honest state of the table is that a dozen rows are
waiting for the same treatment `text/kerning` got.

## Unimplemented layout modes

Each of these lays out as a plain block. That is deliberate — a wrong box keeps the content on the
page and shows up as a geometry difference, where dropping the element would leave nothing for the
corpus to measure — and every one reports through `HtmlOptions.OnDiagnostic`, so a document using
one says so at conversion time rather than only looking wrong afterwards.

A report is not a substitute for a scenario: it says the construct was not rendered properly, not
by how much. **No scenario in the corpus contains `display: grid`**, so how wrong that one is
remains unmeasured.

- **Grid.** The most valuable remaining piece, and the substrate is now more than block: flexbox
  brought the axis mapping, the line collection and the free-space distribution that a grid's track
  sizing wants, and `IntrinsicWidths` already answers for a container whose children are sized
  together rather than one at a time.
- **Multi-column.** `column-count` and `column-width` are reported and lay out as one column.

Flexbox came off this list. What is left of it is below, under **Flexbox**, and none of it is a
layout mode falling back to a block.

## Text

- **PDFium truncates the fractional `/W` glyph widths krilla writes.** This entry used to read
  "sub-pixel glyph positioning", blamed accumulated float error against Chrome's 1/64px
  `LayoutUnit`, and proposed positioning each glyph from a rounded-to-1/64 running origin. All three
  are wrong, and the proposed experiment would have changed nothing — which is worth keeping as a
  reminder that a residual with a stated cause is still a guess until something measures the cause.

  What is actually happening: the glyph positions the two PDFs ASK FOR are identical, to within six
  millionths of a pixel across a whole page, and so are the outlines and their hinting instructions,
  byte for byte. Chromium's printer writes an explicit `Td` before every glyph; krilla writes one
  `TJ` array per run and leans on the font's `/W` widths for the base advance. PDFium truncates a
  fractional `/W` to an integer, so at 2048 units per em every glyph loses up to a thousandth of an
  em and the loss accumulates — 1.09px by the end of a line of 32px text.

  Proved by rewriting the `/W` array with the widths already FLOORED, which renders byte-identically
  to the unmodified file: zero differing pixels, only possible if PDFium was flooring them anyway.
  Rounding them instead takes `text/kerning` from 17,339 differing pixels to 2,239.

  **The fix is upstream and there is nothing to do here.** krilla writes the fractional width and
  computes its `TJ` adjustments against the full-precision advance, so the two agree and the PDF is
  correct for any renderer that honours `/W`; it is PDFium that is lossy, and PDFium is what Chrome
  views PDFs with. The fix is to write `/W` as integers AND derive the adjustments from those
  integers, so the corrections absorb the rounding. krilla is pinned at `=0.8.2`, which is the
  newest published version, so this wants an upstream report rather than a bump.

  **It explains some of the table and not all of it.** The error is proportional to the font size,
  and measuring the end-of-line drift on every differing scenario sorts them cleanly: `text/kerning`
  drifts 0.90px, `block/border_styles` 0.66, `page/break_avoid` 0.39, `text/ligatures` 0.36,
  `page/orphans_widows` 0.35, `page/table_header` 0.28, `page/table_footer` 0.22, and a further
  handful between 0.10 and 0.15 — every one of them a scenario carrying text above 16px. The
  scenarios whose text is all 16px measure **0.000px** of drift end to end and still differ on
  several hundred pixels, with the same distribution of small deltas at glyph edges. Those are
  unexplained: the cause they were attributed to has been refuted and nothing has yet measured what
  replaced it.
- **No UAX #14 line breaking.** Break opportunities are spaces, hyphens and dashes, either side of
  an atomic inline, and the cuts `word-break`/`overflow-wrap` ask for. CJK has none of those, so it
  does not wrap at all and overflows instead. REPORTED now, through `UnsupportedText`, so a document
  in one of those scripts says so — but still unmeasured. Only one scenario in the corpus contains
  any non-ASCII text at all (`inline/hyphen_breaks`, for its dashes), and a scenario cannot be added
  without a change to the fixtures. The corpus pins its faces so that both sides load the same
  files, and the Liberation set has no CJK coverage: a scenario would render as `.notdef` here and
  in a fallback face in the browser, so the comparison would measure the fonts rather than the line
  breaking.
- **No automatic hyphenation.** `hyphens` is reported. Soft hyphens are implemented and measured by
  `text/soft_hyphen`; what is missing is a dictionary deciding where a break may fall.
- **No bidirectional resolution.** A run is shaped in one direction, so mixed Arabic or Hebrew with
  Latin comes out in the wrong order. `krilla_font_shape` takes a direction already, so the missing
  piece is the UAX #9 paragraph algorithm above it. Reported through `UnsupportedText`; nothing
  measures it, for the same fixture reason as the line breaking above.
- **Font fallback searches faces, not the system.** `FontSet.Covering` picks a registered face
  covering a character the resolved one lacks, which is what a caller's own font set can answer. A
  character NO registered face covers still renders as `.notdef` — krilla has no font database, so
  there is nowhere else to look, and reaching the host's installed fonts would end the
  reproducibility the whole corpus rests on. It is reported now, which is the useful half: a caller
  whose set is short a script is told which character it was.
- **A document's own `@font-face` rules are reported and not read.** A document that ships its
  fonts — a mail merge with a corporate face, anything exported from a design tool — renders in
  whatever `HtmlOptions.Fonts` happens to hold, and now says so. Two things make honouring them more
  than an afternoon. A font is a document resource with exactly the image policy's exfiltration
  concern, so it needs a resolver and a policy of its own or a deliberate decision to share the
  image ones; and `FontSet` is caller-owned and routinely SHARED across conversions — the corpus has
  one static set for the whole run — so a document's faces have to be an overlay rather than a
  registration, or one document's fonts leak into the next.

## Boxes and painting

- **A box edge landing on a fractional position still leaves a pixel column.** Every rectangle fill
  is snapped now — the block background, the inline fill, the inline edge boxes, the background
  image, the border box and an image draw — and what is left is where two SNAPPED boxes disagree
  about which pixel a shared edge belongs to. `position/absolute` is one column at x=158 between two
  differently coloured backgrounds; `image/inline_flow` is one at an image edge. Snapping in LAYOUT
  units does not guarantee whole DEVICE pixels either, since coordinates round-trip through PDF
  points.
- **`text-decoration-skip-ink` is not implemented**, and deliberately not reported. Chrome
  interrupts an underline around a descender at the property's default of `auto`; doing the same
  needs glyph outlines rather than advances. A report would fire on every underlined document ever
  converted, so `text/decoration_style` records it as a named residual instead — sixteen pixels, at
  two `p` descenders and a comma.
- **Two overlapping strokes double their own alpha.** A patterned border edge is stroked corner to
  corner rather than mitred, and a collapsed table's grid lines run half a crossing line past each
  end so that no junction is left unpainted — both are deliberate, and both composite to more than
  the colour asked for once the colour is translucent. `block/translucent` keeps its dashed row to
  one side and names the table's junctions for this reason. The same is true of two antialiased
  trapezia meeting on a mitre, which is the residual both `bevelled_borders` scenarios record.
- **A rounded BLOCK border whose edges disagree about a colour leaves a seam at each corner.** The
  ring is drawn through the trapezium clip a mitred edge already uses, so the split runs from the
  outer corner to the inner one — which is where a browser hands over, to within a pixel. Chromium
  carries one colour a fraction further around the arc, so `block/border_radius_sides` sits at
  0.9988 with its geometry exact. The same mechanism as the mitre residual above, and not reported:
  a patterned edge on a rounded corner IS reported, and this is the case where the corner is drawn
  and is a pixel out.
- **A gradient's ramp is quantised differently from Chrome's.** No pixel in `block/gradients`
  differs by more than two of 255 and `#stops` and `#hard` are exactly identical, so this is a
  rounding difference along the ramp rather than a geometry one. Probably not worth chasing; it is
  listed so that the high `AE` is recognisable as expected rather than as a regression.
- **A vertical dotted border edge follows a construction this does not reproduce.** A horizontal
  one fits its pattern into the whole side, corner to corner, and the flush rule reproduces it
  exactly; a vertical one does not. On a 30px box Chromium's left edge carries five dots at a pitch
  of 6.25 starting a pixel below the top corner, where the same rule applied to the full side gives
  six at 5.4. Probed at four heights, with and without adjacent horizontal borders, and no inset of
  the side reproduces it — the end of such an edge carries a solid square no dot in the sequence
  accounts for, which is the shape of Blink filling a rect at each endpoint of a dashed line and
  dashing between them. `block/border_styles` records it and is otherwise exact.

## Flexbox

Implemented and measured: eight scenarios in `flex/`, plus `page/flex_break`, all geometry-exact
against Chrome. What is left is small and each entry says whether anything measures it.

- **An absolutely positioned child's static position is the container's content-box ORIGIN.** CSS
  puts it where the child would be "if it were the sole flex item", which runs it through
  `justify-content` and `align-items`; this uses the origin. The two agree wherever the container's
  alignment is the initial value and wherever the child declares both offsets, which is every
  arrangement anyone writes — so `flex/nested`'s `#anchored` cannot tell them apart, and nothing
  else can either. Not reported, there being no declaration to hang it on: the child's own offsets
  are honoured exactly as declared.
- **A column item with a DECLARED height takes its declaration as the content size suggestion.**
  CSS Flexbox §4.5 wants the smaller of the specified suggestion and the MIN-CONTENT size, and the
  natural height this reuses is the one that layout already produced — which for a declared height
  is the declaration. So such an item will not shrink below its declared height even where its
  content would allow it. Measuring the other answer needs a second layout with the declaration
  suppressed, and nothing in the corpus reaches it.
- **The three margin-box-style edge cases of `align-content` on a single-line container are
  untested.** `align-content` is applied whenever the container may wrap, which is CSS's own rule —
  a `flex-wrap: wrap` container holding one line IS multi-line — and `flex/wrap`'s `#spread` is the
  only scenario that exercises it, with two lines.
- **`visibility: collapse` on a flex item is not implemented**, the same way it is not for a table
  row and for the same measured reason: Chrome disagrees with itself between its screen layout and
  its printer, so no engine behaviour is exact on both of the corpus's measurements.
- **Fragmentation is the ordinary line-based one.** `page/flex_break` measures it and it agrees with
  Chromium, because CSS Flexbox §11 fragments a row container's items in parallel and the engine's
  existing candidates already fall at the same place. What is NOT implemented is the rest of §11:
  `break-inside` on a flex ITEM is honoured through the ordinary path, but a flex line does not
  reserve room for itself, so a container whose items each carry `break-inside: avoid` and which
  straddles a boundary breaks between the items rather than moving the line.

## Tables

- **A rowspan cell takes part in the row it STARTS in and nothing else.** CSS 2.1 §17.5.4 aligns it
  there, which is what this does, but its own height then goes through the ordinary spanning
  shortfall — so a spanning baseline cell whose content reaches below the last row it covers is not
  measured against that row's baseline. Nothing measures it, and `table/cell_baseline` deliberately
  stays away from it.

## Floats

- **A cleared FIRST child still collapses its margin out when the float it clears is declared in
  its own parent ahead of it.** The rule itself is implemented and `float/clearance` measures it;
  what is left is the arrangement where the two passes disagree about whether the float exists.
  `LeadingMargin` runs before the parent is laid out, and a float declared in that parent is placed
  during it — so while a margin is still escaping through the parent's top edge, a float at or
  before the child turns the clearance test off, because the ancestor asked the same question
  without it and applying the margin twice is worse than applying it at the wrong level.
  Reconciling them needs the second pass §9.5.2's *hypothetical* position exists to avoid.

- **The band a line is given is sampled over the strut height**, not the line height it turns out
  to have. A line made taller by an image or a larger inline font could overlap a float that begins
  a little below its top edge. Sampling correctly means laying the line out twice, which is why it
  was left; the strut is the block's own line-height and is right whenever the line has no taller
  content in it.

## Positioning

- **A `position: fixed` box with neither `top` nor `bottom` is painted once**, where flow put it.
  The anchored case repeats on every page and `page/fixed_repeat` measures it; this one cannot,
  because its position is a position in the DOCUMENT rather than on a page and repeating it would
  put a box whose flow position is on page three off the bottom of every page. Chromium draws such
  a box twice — once where flow put it, and again at that page-relative offset on every later page
  — which is measured in `page/fixed_repeat`'s notes and deliberately not matched. Reported.

## Pagination and paged media

- **A run's lines are counted from the BLOCK's start rather than from the page top, and the two
  cannot be told apart.** Counting from the page was written and then reverted, because nothing can
  observe the difference: a block that continues onto a page starts its lines AT that page's top, so
  the run on it is the whole page's worth, and `orphans` is satisfied either way. The one
  arrangement where the counts differ is a block whose middle page holds fewer lines than `orphans`
  asks for — which needs the run on that page to start below the page's top, and a continuing block
  never does. The guard that refuses a move leaving the page empty then makes the two readings
  identical. Measured rather than reasoned, and left as it is.

- **The three margin boxes in a strip are not divided between.** CSS Paged Media §5.3 sizes them
  from their content and shares out the remainder; each is given the whole strip here and placed by
  its own alignment. The two agree wherever one box in a strip has content, and differ only when
  two long ones share a strip, where this lets them overlap. Unmeasurable — Chromium implements no
  margin boxes at all, so there is no reference — and not reported, since nothing an author could
  act on distinguishes the readings.
- **An `avoid` at a box edge moves the break to the DECLARING box's own edge**, rather than to the
  nearest earlier break opportunity. Those are not the same point — the nearest opportunity is a
  line inside the box, and breaking there splits the very box the property was written to keep
  whole — so the destination is recorded rather than searched for. A browser searches, and the two
  answers differ when the box has more than one line in it.

## Structure and metadata

`HtmlOptions.Tagged` builds a structure tree now, in reading order, and everything that is not
content is marked as an artifact, so no operator puts ink on the page from outside one or the other.
A cell says how far it spans and which headers describe it, a counter marker is the item's `Lbl`,
and `role`, `aria-label`, `aria-labelledby` and `aria-describedby` are read. What is left:

- **It is OFF by default.** Turning it on changes the bytes of every document, so it wants a round
  of its own — and it is the last thing here that is a decision rather than work.
- **`aria-hidden` is not read**, and it is the one ARIA attribute left that changes what a reader
  meets rather than what it is called. A document hiding a decorative run from a screen reader
  still has that run tagged as content here. Honouring it is not a mapping but a PAINTING change:
  the run has to open an artifact span instead of a content one, so the hidden-ness of an ancestor
  has to reach `PdfPainter` the way `visibility` does. Nothing measures it.
- **`<th abbr>` lands in `/Alt` rather than in `/Short`.** PDF 2.0 has a field meaning exactly what
  HTML's attribute means — an abbreviated form of the header, announced where repeating the whole
  of it would be tedious — and krilla exposes no setter for it, so the short form is announced in
  place of the content everywhere instead of only where the header is referenced. A one-line
  addition to `rust/crates/krilla-capi/src/api/tag.rs` if krilla's own `TableCell` grows it.

## Diagnostics

`HtmlOptions.OnDiagnostic` reports constructs the engine recognised and did not render the way a
browser would, and the invariant it carries is that a conversion reporting nothing rendered
everything the way a browser would. That invariant is only as good as the table behind it, so what
the table does NOT cover belongs here:

- **Below the declaration level, only three things report.** `UnsupportedText` covers the missing
  UAX #14 line breaking, the missing bidirectional resolution, and a character no registered face
  covers — each a property of the CHARACTERS rather than of a declaration anyone wrote, so no amount
  of scanning the cascade would have found them. What is still silent there is the `ex` and `ch`
  approximation inside `@page`, and sub-pixel glyph positioning; neither has a character to hang a
  report on, and the second would fire on every document ever converted.
- **Structural gaps do not report.** A percentage height inside an inline-block, and a rowspan cell
  aligned on the wrong row's baseline: each is a shape the engine does not produce rather than a
  value it declined to honour, and there is no site in the cascade scan to hang one on. An
  unanchored `position: fixed` box reports only because the declaration itself is a site.
- **A percentage height resolving as `auto`** is correct whenever the containing height is
  indefinite and wrong otherwise, and which of those applies is a layout result rather than a
  declaration — so reporting it would fire on documents that are perfectly correct, which is the one
  thing the table must not do.
- **Origin is not testable**, and it now has two consumers rather than one: what a diagnostic may
  report, and which user-agent declaration a presentational attribute may beat.
  `ComputeCascadedStyle` does not say whether a declaration came from
  the document or from the default stylesheet, so a UA rule the author never wrote could only be
  kept quiet by naming the element. Nothing needs it today — `hr` was the single case, exempted from
  border-style reporting, and implementing the four bevelled styles removed the entry that would
  have fired. Worth remembering that an exemption by element name is a sign the property is
  unimplemented rather than a sign the report is wrong.
- **A LOGICAL declaration wins over a physical one**, whatever order they were written in, because
  the two never reach a common slot and nothing can say which came later. It is the right way round
  — a physical value is on practically every element of every document, `* { margin: 0 }` being how
  a stylesheet begins — and it is still not the cascade's rule. The same shape as the entry below.
- **A pseudo-element's own declarations are separated by VALUE, not by origin**, which is the same
  limitation seen from the other side. Its cascade carries the host's declarations too, so a
  property is treated as the pseudo's when it differs from what the host's cascade says — and a
  pseudo declaring exactly what its host declares therefore loses the declaration. `display` is
  recovered from the rules instead, that being the property the two agree on for every block host;
  everything else takes the heuristic.
- **A value nothing PARSES is a value nothing can report.** The audit that diffs what
  `StyleResolver` reads against what `UnsupportedCss` lists cannot see a syntax the resolver does
  not recognise — `calc()` was that case, falling through to the unparseable fallback with no
  diagnostic possible. Re-run the audit when adding properties, and remember it has this blind
  spot.
- **The audit has to be run against the CASCADE, not against the table.** Diffing what
  `StyleResolver` reads against what `UnsupportedCss` lists finds a property the engine claims and
  gets wrong; it cannot find one that neither file mentions. Enumerating what
  `ComputeCascadedStyle` actually hands back is the pass that finds those, and the last one found
  twenty-three — including `translate`, `rotate` and `scale`, which are not shorthands for
  `transform` and reach no longhand of it, so a document in the modern spelling moved nothing and
  was told nothing.
- **Neither audit looks at the MARKUP.** Both compare CSS against CSS, so an element the engine
  lays out wrongly reports nothing unless a property was involved. The pass that finds those is
  reading `UserAgentStyles` against the HTML Standard's rendering section, and it found two in one
  sitting: `<font>` had no `display` at all, so every run it wrapped went on a line of its own, and
  `<wbr>` reached the tokeniser as an empty run and offered no break. Neither reported anything,
  and neither could have — there is no declaration to hang a report on.

## Known limitations that are workarounds, not bugs

Each is documented in `CLAUDE.md`; they are listed here so that removing one is recognisable as
work rather than as tidying.

- **AngleSharp compares specificity across cascade origins**, where the specification resolves
  origin first. A consumer reset relying on `* { margin: 0 }` will not clear the UA margins on
  `body` and `p`. `Inputs/flatten.css` names elements explicitly to work around it. Fixing it
  properly means filtering declarations by origin, which `ComputeCascadedStyle` does not expose.
- **AngleSharp drops some declarations rather than passing the value through**, so they can be
  neither honoured nor reported. The audit that diffs what the cascade hands back against what the
  engine reads is also what finds these, because a dropped declaration is invisible from both
  sides. Found so far: `revert`, `text-overflow`, the `min-content`/`max-content`/`fit-content`
  sizing keywords, `aspect-ratio` given a single number, `overflow-wrap: anywhere`, `overflow:
  clip`, `recto`/`verso` on both break spellings, `white-space: break-spaces` AND
  `white-space-collapse: break-spaces`, `background-position` given four components,
  `caption-side: inline-start`, `text-justify: none` and `inter-character`, `filter`, `clip-path`,
  `shape-outside`, `zoom`, `text-emphasis`, `background-clip: text`, `text-underline-position`,
  `font-variant-caps` and `font-variant-ligatures`, and — from the flex round — `flex-basis` given
  `min-content`, `max-content` or `fit-content`. SIX more were dropped and are now recovered from
  the stylesheet's own text by `CssSource`: `string-set`, `page`, a `content` value carrying
  `string()`, a `::before`'s `display`, `flex: none`, and the CSS Box Alignment keywords —
  `start`, `end`, `normal` and `space-evenly` — on `justify-content`, `align-items`,
  `align-content` and `align-self`. The whole of `@page` except its margins goes the same
  way — its `size`, its selector, and its margin box at-rules.
- **AngleSharp TRANSPOSES `gap`'s two values.** CSS writes the shorthand
  `gap: <row-gap> <column-gap>`; the cascade hands back the first value as `column-gap` and the
  second as `row-gap`, which is the picture rotated rather than a number out. It cannot be
  recognised from the cascade either — what comes back is byte-for-byte what an author writing both
  longhands column-first produces — so the shorthand is recovered from the stylesheet's own text,
  and only for a document that contains one. The one-value form is correct as it stands, leaving
  `row-gap` empty, which means "the same as the other". `flex/wrap`'s `#gapped` is what measures it.

- **AngleSharp does not ALIAS two spellings of one property**, nor expand a shorthand into
  longhands the engine reads, which is a quieter version of the same problem: `word-wrap` comes back
  under its own name and leaves `overflow-wrap` empty, exactly as `page-break-before` does beside
  `break-before`, and `white-space` leaves `white-space-collapse` and `text-wrap` empty while those
  leave it empty in turn. Both spellings of all three are read now. The logical box properties are
  the same shape again — `margin-inline` reaches no physical property at all — and are read from
  their own names. `background-repeat` is the one that goes the other way: it is SPLIT into
  `-x` and `-y` and the shorthand reserialised, which folds `repeat no-repeat` onto `repeat-x` and
  has no spelling at all for `round no-repeat`, so the longhands are read first.
- **A presentational attribute applies only where the cascade is EMPTY or holds the user-agent's
  own value.** HTML puts `<table width>` and the rest in an origin between the user-agent sheet and
  the author's, and `ComputeCascadedStyle` does not say which origin a value came from — so
  `PresentationalHints.defaults` names the handful of user-agent declarations a hint is allowed to
  beat and compares against those. The cost is that an author restating the user-agent's own value
  loses to the attribute: `table { border-spacing: 2px }` in a document's own stylesheet does not
  beat `cellspacing="0"`, where in a browser it would. The same separated-by-VALUE heuristic a
  pseudo-element's own declarations go through, and the same underlying limitation.
- **A generic font family cannot be pinned in the corpus**, because a generic name is not legal as
  an `@font-face` family, so "does `<pre>` default to monospace" is not measurable here.
- **A `::before` or `::after` rule's `display` is read from the stylesheet's own rules**, because
  the cascade cannot report it: AngleSharp hands a pseudo-element the HOST's declarations too, and
  measured, a `<div>` whose `::before` declares nothing but `content` comes back with
  `display: block`. The scan is bounded the way the `@page` one is — only style rules, only a
  selector naming the pseudo, only the `display` declaration — and shares its two limitations:
  specificity is not compared, and media queries are not evaluated.
- **`line-height: normal` imitates Chrome's rounding** rather than following a specification,
  because CSS explicitly leaves the value to the user agent. If the reference browser ever changes,
  this is the first thing that will move. The same is true of every number in `ListMarkers`.

## Infrastructure

- **The corpus references are generated on one platform.** Regenerating on the machine that
  produced them is known to be byte-identical, so the generator is at least deterministic — but
  that is the lesser half. The claim that matters is still untested: generating all 153 on a second
  machine, with a different Chromium build, and diffing the PNGs. Until then a platform-specific
  difference would look like a layout regression.
- **`Krilla.Html` is never packed or published by CI.** It packs perfectly well locally — the
  release job simply builds `src/Krilla/Krilla.csproj` alone, so only `Krilla` reaches `./nugets`
  and only `Krilla` is pushed. `IntegrationTests` references `Krilla` too, so nothing tests that
  `Krilla.Html` resolves from a packed nupkg the way `Krilla` does.
- **Shaping allocates per inline item.** `ShapedText` shapes once and slices, which is the right
  shape, but nothing caches across items — a document repeating the same short string in many
  elements reshapes it every time. Measure before optimising, and note that nothing currently
  measures it: the benchmark project covers list marker text alone, and no benchmark converts a
  whole document.
