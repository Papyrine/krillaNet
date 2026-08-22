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

74 scenarios across 10 categories. **Box geometry matches Chrome exactly on every one** — zero
positional and zero size difference, and nothing unmatched — and 53 read SSIM 1.0000, of which 36
are pixel-identical outright.

The twenty-one that read below 1.0000, with the cause of each. None is a mystery, and none should
be "fixed" by regenerating a baseline:

| Scenario | AE | SSIM | Cause |
| --- | --- | --- | --- |
| `ua/hr` | 0.0023 | 0.9911 | Chrome's `inset` border painted solid |
| `text/kerning` | 0.0201 | 0.9945 | Sub-pixel glyph positioning, below |
| `text/ligatures` | 0.0099 | 0.9982 | Same |
| `page/break_between_lines` | 0.0017 | 0.9996 | Same |
| `table/anonymous` | 0.0003 | 0.9996 | Box edge at a fractional position, below |
| `page/float_break` | 0.0023 | 0.9997 | Sub-pixel glyph positioning |
| `page/table_break` | 0.0003 | 0.9997 | Box edge at a fractional position |
| `block/min_width` | 0.0009 | 0.9998 | Sub-pixel glyph positioning |
| `image/inline_flow` | 0.0007 | 0.9998 | Box edge at a fractional position |
| `page/multi_page_flow` | 0.0016 | 0.9998 | Sub-pixel glyph positioning |
| `block/box_sizing` | 0.0004 | 0.9999 | Same, on the one line beside a float |
| `float/shrink_to_fit` | 0.0000 | 0.9999 | Box edge at a fractional position |
| `inline/inline_block` | 0.0002 | 0.9999 | Sub-pixel glyph positioning |
| `inline/inline_block_sizing` | 0.0002 | 0.9999 | Same |
| `inline/text_indent` | 0.0005 | 0.9999 | Same |
| `link/fragment`, `link/wrapped` | 0.0001 | 0.9999 | Same |
| `page/tall_block` | 0.0004 | 0.9999 | Same |
| `position/absolute` | 0.0005 | 0.9999 | Box edge at a fractional position |
| `table/spacing_borders` | 0.0003 | 0.9999 | Same |
| `ua/blockquote_pre` | 0.0005 | 0.9999 | Sub-pixel glyph positioning |

Seventeen more read SSIM 1.0000 while still differing on a scattering of antialiased pixels. Each
is one of the two causes above, and none exceeds 40 levels of grey out of 255.

## Unimplemented layout

Each of these currently lays out as a plain block. That is deliberate — a wrong box keeps the
content on the page and shows up as a geometry difference, where dropping the element would leave
nothing for the corpus to measure — but no scenario covers them, so the corpus still does not
measure how wrong.

They are no longer silent, which is the part that changed: every item below reports through
`HtmlOptions.OnDiagnostic`, so a document using one says so at conversion time rather than only
looking wrong afterwards. A report is not a substitute for a scenario — it says the construct was
not rendered properly, not by how much — but it does mean the dangerous case, an unmeasured gap
nothing announces, no longer applies to anything in this section.

- **Flexbox**, then **grid**. The most valuable next piece now that positioning is in, and the
  block substrate they wanted — tables, floats and positioned boxes — is underneath them.

## Text

- **Box edges at fractional positions are not snapped.** A border or background edge that lands on
  a fractional pixel is painted antialiased, where Chrome snaps a box decoration to whole device
  pixels first. Measured by `table/spacing_borders` and `image/inline_flow`, and it shows up in
  tables more than anywhere else because column widths are fractional by nature. Fixing it means
  snapping at paint time, which touches every background and border in the corpus — worth doing
  deliberately rather than alongside something else.
- **Sub-pixel glyph positioning.** The largest residual left: `text/kerning` differs on 1.6% of
  pixels, and no whole-pixel shift improves it, so it is not an offset. The suspicion is
  accumulated float error against Chrome's `LayoutUnit`, which is a fixed-point 1/64px. Worth
  measuring before changing anything — if that is the cause, positioning each glyph from a
  rounded-to-1/64 running origin should close it.
- **No UAX #14 line breaking.** Break opportunities are at spaces only, so CJK does not wrap at
  all and overflows instead. Also no hyphenation. Nothing measures this; a scenario would fail
  immediately, which is the point of adding one.
- **No bidirectional resolution.** A run is shaped in one direction, so mixed Arabic or Hebrew
  with Latin comes out in the wrong order. `krilla_font_shape` takes a direction already, so the
  missing piece is the UAX #9 paragraph algorithm above it.
- **No font fallback.** A run resolves to one face; a character that face lacks renders as
  `.notdef`. This interacts with shaping — fallback means splitting a run at coverage boundaries
  and shaping each piece separately.
- **`text-decoration`** covers underline only. Overline and line-through are parsed as "not
  underline" and drawn as nothing.
- **Every `font-size` keyword falls through to the inherited size.** `medium`, `large`, `smaller`
  and the rest are not lengths AngleSharp resolves, so none arrives as one. `<small>` and `<big>`
  are unaffected — the cascade turns the default stylesheet's sizes for those into real lengths
  before layout sees them — so this is only reachable by writing a keyword directly, which is
  ordinary enough CSS to be worth closing. Reported through `OnDiagnostic`. Fixing it means the
  CSS absolute-size table, measured against Chrome rather than assumed, plus a ratio for
  `smaller`/`larger`.
- **`ex` and `ch` units are approximated** at half an em. Exact values need the face's x-height
  and the advance of `0`, which means threading a face into length resolution.

## Boxes and painting

- **Inline elements generate no box.** `<b>`, `<i>`, `<a>` and `<br>` contribute runs to a line
  rather than a `LayoutBox`, so the corpus records them as unmatched — 2 in
  `inline/line_height_normal`, 4 in `inline/nested_inline`, 1 to 2 across `link/`. Inline images
  already report geometry this way, so the mechanism exists. Doing it properly means an inline box
  can carry a background, border and padding, which none currently can.
- **`border-style` is solid or nothing.** Dashed, dotted, double, groove and the rest all paint
  solid.
- **`list-style-position: inside` renders as `outside`.** The marker is drawn in the margin instead
  of in the text flow, because doing it properly means shortening the item's first line, which is a
  change to inline layout rather than to painting. No scenario measures it, though it does report
  through `OnDiagnostic`. Drawing the marker at the content edge without shortening the line would
  overlap the text, which is why it renders as `outside` rather than approximately right.
- **`list-style-image` and the `type` content attribute are ignored.** `<ol type="a">` numbers in
  decimal, because HTML's presentational-hint mapping for it is not applied — the same gap
  `<img width>` used to have, and fixable the same way. Both report through `OnDiagnostic`.
- **An empty `<li>` is zero high**, where a browser gives it a line box for its marker to sit on.
  The marker is still drawn, at the baseline that line would have had, so it lands on top of
  whatever follows. Nothing measures it.
- **No `overflow` handling.** Content larger than a box with an explicit height paints outside it
  and is never clipped, because only the page box clips.
- **Percentage heights resolve as `auto`.** Correct whenever the containing height is indefinite,
  which it usually is in paged media, but wrong for a box inside one with a definite height.
- **No background images or gradients**, no `opacity`, no `transform`, no `border-radius`.

## Tables

Implemented: the automatic and fixed column algorithms, row and column spanning, row groups in
render order, captions, the separated border model with `border-spacing`, and vertical alignment.
Box geometry matches Chrome exactly across the five `table/` scenarios. What is missing:

- **`border-collapse: collapse` lays out as separated.** A different model rather than a variation
  on this one: collapsed borders are shared between neighbours, half of each sits outside the cell,
  and conflicts between a cell's border and its table's resolve by a precedence rule. It is what
  most real stylesheets set, so this is the largest remaining table gap. No scenario measures it,
  though it does report through `OnDiagnostic`.
- **`<col>` and `<colgroup>` are ignored.** They generate no box and their `width` does not reach
  column sizing, so a document that sizes its columns that way gets automatic widths instead. They
  are also boxes the browser reports and this does not, so a scenario using them would show
  unmatched boxes rather than a geometry difference. Reported through `OnDiagnostic`.
- **`vertical-align: baseline` on a cell renders as `top`.** Aligning a row's cells against each
  other's first baselines needs a pass that does not exist. It is not the default — the user-agent
  stylesheet makes cells `middle` — so this is only reachable by asking for it.
- **A table is paginated like any other box.** A table taller than the page breaks between lines
  wherever the scan lands, so a row can be cut in half and a `thead` does not repeat on the second
  page. Repeating headers is the feature people expect from HTML-to-PDF conversion of a long table,
  and it needs pagination to know what a table is.
- **Presentational attributes are not applied.** `<table width>`, `<td bgcolor>`, `<tr height>`,
  `<p align>`, `<font color>` and `<ol type>` all reach the cascade as nothing, because AngleSharp
  performs none of HTML's presentational-hint mapping. The same gap `<img width>` used to have, and
  fixable the same way — `BoxBuilder.WithAttributeSize` is the shape of the fix. `UnsupportedAttributes`
  holds the list, so every one of them reports through `OnDiagnostic` until then. Worth closing
  rather than dismissing as legacy markup: reporting tools and mail merges emit exactly this.

## Floats

Implemented: placement on both sides, stacking and descent, shrink-to-fit width, `clear` on blocks
and on floats, line boxes shortened by the floats they overlap, lines descending past a float that
leaves them no room, and containment by the box establishing the formatting context. Box geometry
matches Chrome exactly across the four `float/` scenarios. What is missing:

- **A float declared part-way through inline content is placed at the top of its block**, where a
  browser puts it on the line being built when it was reached. The common arrangements are exact —
  a float before the text, or as a child of a block whose other children are blocks — and this is
  the one that is not. It shows as the float sitting higher up the page than it should, taking a
  line or two of text with it. Nothing measures it.
- **Nothing establishes a formatting context except the root, a float and a table cell.** CSS also
  gives one to `overflow` other than `visible`, to `inline-block`, and to a table — and the
  clearfix idiom that every real stylesheet uses to contain floats is `overflow: hidden` on the
  parent. Until that works, a document written to that idiom has floats hanging out of the box that
  was supposed to hold them.
- **A block beside a float is never pushed aside**, which is right for an ordinary block and wrong
  for one that establishes a formatting context: CSS 2.1 §9.5 says a table, a block-level REPLACED
  element or an `overflow: hidden` block must not overlap a float, and narrows or moves it instead.
  Measured while writing `block/box_sizing`, whose `<img>` Chrome pushes to the right of a float
  that is still hanging and this engine leaves under it — which is why that scenario puts its float
  last. Mostly blocked on the point above, though the replaced case needs no formatting context of
  its own and could be closed on its own.
- **Clearance is applied after the collapsed margin rather than as a quantity of its own.** CSS 2.1
  §9.5.2 introduces clearance separately and stops the margin collapsing through it. The difference
  appears only when a cleared box carries a margin large enough to clear the float unaided, which is
  why `float/clear` stays away from it.
- **The band a line is given is sampled over the strut height**, not the line height it turns out to
  have. A line made taller by an image or a larger inline font could overlap a float that begins
  a little below its top edge. Sampling correctly means laying the line out twice.
## Positioning

Implemented: `relative` with all four offsets, `absolute` against the nearest positioned ancestor
or the page, the static position for auto offsets, shrink-to-fit and left-plus-right widths,
per-axis percentage offsets, and CSS 2.1 Appendix E paint order. Box geometry matches Chrome
exactly across the three `position/` scenarios. What is missing:

- **`position: fixed` is placed once rather than repeated on every page.** Its geometry against the
  page is right, so the difference is entirely about paged media: CSS repeats a fixed box on each
  page, which is what would make it usable for a running header. Reported through `OnDiagnostic`.
  This is the one gap in this section with a real use behind it.
- **No `z-index`, and nothing establishes a stacking context.** Two positioned boxes that overlap
  are ordered by their position in the tree, so a document that layers deliberately gets the
  ordering it wrote rather than the one it asked for. Nothing measures it.
- **Auto margins on an absolute box resolve to zero rather than centring it.** CSS centres a box
  whose width and both offsets are given and whose margins are both auto, which is one of the two
  standard centring idioms. Nothing measures it.
- **An absolute box does not paginate.** It is placed once at a document position, so one taller
  than the remaining space on its page is cut at the page edge rather than continued. The same is
  true of a float, and both are the same missing piece.
- **`min-height` and `max-height` are not read**, on positioned boxes or on anything else, so an
  absolute box sized between two offsets ignores them.

## Pagination

- **No `break-before` / `break-after` / `break-inside`**, and no orphans or widows. A break falls
  wherever the line-based scan puts it, so a heading can end a page with its paragraph overleaf.
- **No `@page` margin boxes**, so no running headers or footers and no page numbers. This is one
  of the commonest reasons to convert HTML to PDF at all, and it needs a decision about how to
  express content that repeats per page.

## Structure and metadata

- **No tagged PDF.** Krilla has full tagging support — `SetTagTree`, `Tagging.cs`, and
  `AddTaggedLink` is already used by nothing here — and HTML carries exactly the semantics a tag
  tree wants: headings, lists, tables, figures, alt text. This is a genuine differentiator against
  other HTML-to-PDF libraries and nothing about it is blocked.
- **`alt` text is discarded.** An image that fails to resolve generates no box, matching a browser
  with no alt text, but an image that has alt text should carry it into the tag tree.
- **No document outline** from headings, though `SetOutline` exists and a heading tree maps onto it
  directly.

## Diagnostics

`HtmlOptions.OnDiagnostic` reports constructs the engine recognised and did not render the way a
browser would, and the invariant it carries is that a conversion reporting nothing rendered
everything correctly. That invariant is only as good as the table behind it, so what the table does
NOT cover belongs here:

- **Nothing below the declaration level reports.** Missing UAX #14 line breaking, bidirectional
  resolution and font fallback are properties of the text, not of a declaration anyone wrote, so no
  amount of scanning the cascade finds them. A document in Arabic converts silently and wrongly.
  The same is true of the `ex` and `ch` approximation and of sub-pixel glyph positioning.
- **Structural gaps do not report.** An inline element generating no box, an empty `<li>` being
  zero high, a table paginating between lines rather than between rows, `@page` being ignored
  outright: each is a shape the engine does not produce rather than a value it declined to honour,
  and there is no site in the cascade scan to hang them on.
- **A percentage height resolving as `auto`** is correct whenever the containing height is
  indefinite and wrong otherwise, and which of those applies is a layout result rather than a
  declaration. Reporting it from `StyleResolver` would fire on documents that are perfectly correct,
  which is the one thing the table must not do.
- **Origin is not testable.** `ComputeCascadedStyle` does not say whether a declaration came from
  the document or from the default stylesheet, so a UA rule the author never wrote can only be kept
  quiet by naming the element. `hr` is the single case that needs it today; a second one would be a
  reason to look for a real fix rather than to add another exemption.

## Known limitations that are workarounds, not bugs

Each is documented in `CLAUDE.md`; they are listed here so that removing one is recognisable as
work rather than as tidying.

- **AngleSharp compares specificity across cascade origins**, where the specification resolves
  origin first. A consumer reset relying on `* { margin: 0 }` will not clear the UA margins on
  `body` and `p`. `Inputs/flatten.css` names elements explicitly to work around it. Fixing it
  properly means filtering declarations by origin, which `ComputeCascadedStyle` does not expose.
- **A generic font family cannot be pinned in the corpus**, because a generic name is not legal as
  an `@font-face` family, so "does `<pre>` default to monospace" is not measurable here.
- **`line-height: normal` imitates Chrome's rounding** rather than following a specification,
  because CSS explicitly leaves the value to the user agent. If the reference browser ever changes,
  this is the first thing that will move.

## Infrastructure

- **The corpus references are generated on one platform.** Regenerating all 41 on the machine that
  produced them is now known to be byte-identical, so the generator is at least deterministic — but
  that is the lesser half. The claim that matters is still untested: generating on a second machine,
  with a different Chromium build, and diffing the PNGs. Until then a platform-specific difference
  would look like a layout regression.
- **`cargo deny` and `cargo about` have not been run against the rustybuzz dependency locally.**
  Both should be no-ops — the lock diff adds no package — but only CI has confirmed it.
- **`Krilla.Html` is not in the release pack job** and has no `IntegrationTests` coverage, so
  nothing yet tests that it resolves from a packed nupkg the way `Krilla` does.
- **Shaping allocates per inline item.** `ShapedText` shapes once and slices, which is the right
  shape, but nothing caches across items — a document repeating the same short string in many
  elements reshapes it every time. Measure before optimising.
