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

122 scenarios across 10 categories, 1387 element boxes matched. **Box geometry matches Chrome
exactly on every one** — worst offset 0.00px, worst size 0.00px, and nothing unmatched — and 88
read SSIM 1.0000, of which 60 are pixel-identical outright. The other 28 differ on a scattering of
antialiased pixels, which is what `AE` is there to show.

Thirty-four read below 1.0000. None is a mystery, and none should be "fixed" by regenerating a
baseline:

| Scenario | AE | SSIM | Cause |
| --- | --- | --- | --- |
| `page/table_header` | 0.0119 | 0.9900 | Sub-pixel glyph positioning at 24 and 30px; page three is identical |
| `block/border_styles` | 0.0034 | 0.9906 | Dash phase restarts at each corner, below |
| `ua/hr` | 0.0023 | 0.9911 | `inset` painted solid, below |
| `text/kerning` | 0.0201 | 0.9945 | Sub-pixel glyph positioning, below |
| `text/ligatures` | 0.0099 | 0.9982 | Same |
| `block/shadows` | 0.0057 | 0.9985 | Antialiasing on `#rounded`'s corner; no pixel differs by more than 2 of 255 |
| `image/svg` | 0.0028 | 0.9988 | Sub-pixel glyph positioning in the `<text>` inside the picture |
| `page/fixed_repeat` | 0.0039 | 0.9992 | Sub-pixel glyph positioning |
| `page/break_between_lines` | 0.0017 | 0.9996 | Sub-pixel glyph positioning |
| `page/float_break` | 0.0023 | 0.9997 | Same |
| `table/columns` | 0.0005 | 0.9997 | Unsnapped border at a fractional column edge, below |
| `text/underline_offset` | 0.0002 | 0.9997 | Glyph edges |
| `block/min_width` | 0.0009 | 0.9998 | Sub-pixel glyph positioning |
| `image/inline_flow` | 0.0007 | 0.9998 | Unsnapped image edge at a fractional position |
| `page/break_inside` | 0.0012 | 0.9998 | Sub-pixel glyph positioning on page two; page one is identical |
| `page/multi_page_flow` | 0.0016 | 0.9998 | Sub-pixel glyph positioning |
| `text/word_spacing` | 0.0009 | 0.9998 | Same, across the widened spaces |
| `block/box_sizing` | 0.0004 | 0.9999 | Same, on the one line beside a float |
| `block/counters` | 0.0005 | 0.9999 | Same |
| `block/gradients` | 0.0500 | 0.9999 | Quantisation along the ramp; no pixel differs by more than 2 of 255, and the `AE` means nothing here |
| `block/list_image` | 0.0006 | 0.9999 | Sub-pixel glyph positioning |
| `block/outline` | 0.0006 | 0.9999 | Same |
| `float/overflow_bfc` | 0.0005 | 0.9999 | Same |
| `inline/text_indent` | 0.0005 | 0.9999 | Same |
| `link/fragment` | 0.0001 | 0.9999 | Same |
| `link/wrapped` | 0.0000 | 0.9999 | Same |
| `page/tall_block` | 0.0004 | 0.9999 | Same |
| `position/absolute` | 0.0005 | 0.9999 | Unsnapped border at a fractional position |
| `table/spacing_borders` | 0.0003 | 0.9999 | Same |
| `text/decoration_style` | 0.0002 | 0.9999 | `text-decoration-skip-ink`, deliberate, below |
| `text/font_size_keywords` | 0.0006 | 0.9999 | Sub-pixel glyph positioning |
| `text/letter_spacing` | 0.0005 | 0.9999 | Same |
| `text/text_transform` | 0.0002 | 0.9999 | Same |
| `ua/blockquote_pre` | 0.0005 | 0.9999 | Same |

Six causes cover all thirty-four. Four are property gaps with a fix behind them — the dash phase,
the `inset` family, `text-decoration-skip-ink` and the gradient quantisation — and each is written
up in the sections below. The other two are general: **sub-pixel glyph positioning**, and **a box
edge landing on a fractional position**.

## Unimplemented layout modes

Each of these lays out as a plain block. That is deliberate — a wrong box keeps the content on the
page and shows up as a geometry difference, where dropping the element would leave nothing for the
corpus to measure — and every one reports through `HtmlOptions.OnDiagnostic`, so a document using
one says so at conversion time rather than only looking wrong afterwards.

A report is not a substitute for a scenario: it says the construct was not rendered properly, not
by how much. **No scenario in the corpus contains `display: flex` or `display: grid`**, so how
wrong is still unmeasured.

- **Flexbox**, then **grid**. The most valuable remaining piece by a wide margin, and the block
  substrate they want — tables, floats, positioned boxes, stacking contexts and `overflow`
  formatting contexts — is all underneath them now.
- **Multi-column.** `column-count` is reported and lays out as one column.

## Text

- **Sub-pixel glyph positioning.** The largest residual left: `text/kerning` differs on 2% of
  pixels, and no whole-pixel shift improves it, so it is not an offset. The suspicion is
  accumulated float error against Chrome's `LayoutUnit`, a fixed-point 1/64px — and that suspicion
  now has evidence behind it rather than being a guess, because quantising lengths to 1/64 is what
  took `inline/vertical_align` from 0.9969 to pixel-identical. Positioning each glyph from a
  rounded-to-1/64 running origin is the thing to try, and it should be measured before anything is
  changed: it touches every scenario carrying text, which is most of them.
- **No UAX #14 line breaking.** Break opportunities are spaces, hyphens and dashes, either side of
  an atomic inline, and the cuts `word-break`/`overflow-wrap` ask for. CJK has none of those, so it
  does not wrap at all and overflows instead. Only one scenario in the corpus contains any
  non-ASCII text at all (`inline/hyphen_breaks`, for its dashes), so nothing measures this; a
  scenario would fail immediately, which is the point of adding one.
- **No automatic hyphenation.** `hyphens` is reported. Soft hyphens are implemented and measured by
  `text/soft_hyphen`; what is missing is a dictionary deciding where a break may fall.
- **No bidirectional resolution.** A run is shaped in one direction, so mixed Arabic or Hebrew with
  Latin comes out in the wrong order. `krilla_font_shape` takes a direction already, so the missing
  piece is the UAX #9 paragraph algorithm above it. Nothing measures it.
- **No font fallback per character.** `FontSet.Fallback` is a whole-face fallback — the face used
  when family resolution finds nothing — not a coverage-driven one, and neither `FontSet` nor
  `CharacterMap` tests whether a face covers a given character. A character the resolved face lacks
  renders as `.notdef`. This interacts with shaping: fallback means splitting a run at coverage
  boundaries and shaping each piece separately.
- **`ex` and `ch` are both approximated at half an em** (`CssValues`). `ex` wants the face's
  x-height, which `OpenTypeMetrics` already reads for `vertical-align: middle`, and `ch` wants the
  advance of `0`; both mean threading a face into length resolution, which is why neither is done.
  Not reported, and no scenario uses either unit.

## Boxes and painting

- **`groove`, `ridge`, `inset` and `outset` paint solid.** They need two derived shades of the
  declared colour, which is the whole of why they were left. This is the entirety of `ua/hr`'s
  0.9911 — the rule is a zero-height box drawn by its border, so the border is the whole box. It is
  reported everywhere except on `hr` itself, which `UnsupportedCss.BorderStyles` exempts by name
  because AngleSharp's default sheet makes every plain `<hr>` `inset` and a page carrying no CSS at
  all would otherwise report four times.
- **A dashed border's phase restarts at each corner.** A side whose length is not a whole number of
  periods therefore ends on a partial dash, where a browser redistributes the remainder along the
  side so it ends flush. Measured by `block/border_styles`, and it is the largest residual in the
  corpus: visible at the end of each side and nowhere else, since the dashes along most of every
  edge line up exactly.
- **Borders and replaced content are not snapped to whole pixels.** Backgrounds are — the block
  fill, the inline fill, the inline edge boxes and the background-image origin are all snapped,
  because that is what the browser fills — but the border path has no snap at all, and neither does
  an image draw. That is the second general residual: `table/spacing_borders`, `table/columns` and
  `position/absolute` are borders at fractional positions, and `image/inline_flow` is an image
  edge. It shows up in tables more than anywhere else because column widths are fractional by
  nature. Worth doing deliberately rather than alongside something else, and worth knowing that
  snapping in LAYOUT units does not guarantee whole DEVICE pixels — coordinates round-trip through
  PDF points, so a fractional width can still leave a faint extra column.
- **`text-decoration-skip-ink` is not implemented**, and deliberately not reported. Chrome
  interrupts an underline around a descender at the property's default of `auto`; doing the same
  needs glyph outlines rather than advances. A report would fire on every underlined document ever
  converted, so `text/decoration_style` records it as a named residual instead — sixteen pixels, at
  two `p` descenders and a comma.
- **A gradient's ramp is quantised differently from Chrome's.** No pixel in `block/gradients`
  differs by more than two of 255 and `#stops` and `#hard` are exactly identical, so this is a
  rounding difference along the ramp rather than a geometry one. Probably not worth chasing; it is
  listed so that the high `AE` is recognisable as expected rather than as a regression.
- **Percentage heights resolve as `auto`.** Correct whenever the containing height is indefinite,
  which it is throughout a paginated document, and wrong for a box inside one with a definite
  height. Deliberately not reported — see Diagnostics.

## Tables

- **A `tfoot` does not repeat at the foot of every page.** The header does, and the footer is the
  same idea reflected: it needs a band reserved at the BOTTOM of a continuation page and a second
  offset threaded through the painter, where the header needed one at the top. Unmeasured, and not
  reported either — a `tfoot` on a table that fits on one page is perfectly correct, so a report
  keyed on the element would fire on documents with nothing wrong with them.
- **`vertical-align: baseline` on a cell renders as `top`.** Aligning a row's cells against each
  other's first baselines needs a pass that does not exist. It is not the default — the user-agent
  stylesheet makes cells `middle` — so this is only reachable by asking for it, and it is reported.

## Floats

- **Clearance is applied after the collapsed margin rather than as a quantity of its own.** CSS 2.1
  §9.5.2 introduces clearance separately and stops the margin collapsing through it. The difference
  appears only when a cleared box carries a margin large enough to clear the float unaided, which
  is why `float/clear` stays away from it.
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
- **Auto margins on an absolute box resolve to zero rather than centring it.** CSS centres a box
  whose width and both offsets are given and whose margins are both auto, which is one of the two
  standard centring idioms. Nothing measures it.

## Pagination and paged media

- **`string()` and `string-set` are not implemented**, which is CSS's own way of putting a
  section's heading into a running header. Not reported either, and that is the harder half: the
  cascade DROPS a `content` declaration carrying `string()`, so it comes back empty and is
  indistinguishable from a margin box that declared none. `PageMarginBoxTests` pins the limitation
  so it is not rediscovered as a defect.
- **A named `@page` selector selects nothing.** `@page cover` matches the elements carrying
  `page: cover`, and that property is not read. Reported, and dropped rather than applied to every
  page — a cover sheet's header on every page is worse than none.
- **The three margin boxes in a strip are not divided between.** CSS Paged Media §5.3 sizes them
  from their content and shares out the remainder; each is given the whole strip here and placed by
  its own alignment. The two agree wherever one box in a strip has content, and differ only when
  two long ones share a strip, where this lets them overlap. Unmeasurable — Chromium implements no
  margin boxes at all, so there is no reference — and not reported, since nothing an author could
  act on distinguishes the readings.
- **An INLINE image taller than a page is sliced at the page edge**, where Chrome moves it whole to
  a fresh page and lets it overflow from there. The block-level case is fixed —
  `Paginator.Unbreakable` lists a replaced element alongside a table row — but an inline image is
  not a `LayoutBox`, it hangs off the line, so it goes through the line breaker instead and never
  reaches that list. Unmeasured: `image/svg`'s `#tall` row is block-level, and an inline row for it
  was removed for exactly this reason rather than committed failing.
- **`break-before: avoid` and `break-after: avoid` are reported rather than implemented.**
  `break-inside: avoid` is done, because it names a rectangle to keep together and the slice
  already moves rectangles whole. Avoiding a break at a box EDGE asks for a break to be moved
  somewhere earlier, and the slice has no notion of rejecting a candidate in favour of one further
  back.

## Structure and metadata

- **No tagged PDF.** `Krilla` exposes `DocumentOptions.EnableTagging`, a tag tree and
  `Surface.AddTaggedLink`, and `Krilla.Html` calls none of them. HTML carries exactly the semantics
  a tag tree wants: headings, lists, tables, figures, alt text. The neighbouring structure work has
  landed — the outline from headings, a named destination per `id`, the document title and language
  — so this is the remaining half of it, and it is a genuine differentiator against other
  HTML-to-PDF libraries with nothing blocking it.
- **`alt` text is discarded.** An image that fails to resolve generates no box, matching a browser
  with no alt text, but an image that has alt text should carry it into the tag tree. Nothing in
  the converter reads the attribute, and no corpus scenario sets one.

## Diagnostics

`HtmlOptions.OnDiagnostic` reports constructs the engine recognised and did not render the way a
browser would, and the invariant it carries is that a conversion reporting nothing rendered
everything the way a browser would. That invariant is only as good as the table behind it, so what
the table does NOT cover belongs here:

- **Nothing below the declaration level reports.** Missing UAX #14 line breaking, bidirectional
  resolution and font fallback are properties of the text, not of a declaration anyone wrote, so no
  amount of scanning the cascade finds them. A document in Arabic converts silently and wrongly.
  The same is true of the `ex` and `ch` approximation and of sub-pixel glyph positioning.
- **Structural gaps do not report.** A table not repeating its footer group, and an absolute box's
  auto margins not centring it: each is a shape the engine does not produce rather than a value it
  declined to honour, and there is no site in the cascade scan to hang them on. An unanchored
  `position: fixed` box reports only because the declaration itself is a site.
- **A percentage height resolving as `auto`** is correct whenever the containing height is
  indefinite and wrong otherwise, and which of those applies is a layout result rather than a
  declaration. Reporting it from `StyleResolver` would fire on documents that are perfectly
  correct, which is the one thing the table must not do.
- **Origin is not testable.** `ComputeCascadedStyle` does not say whether a declaration came from
  the document or from the default stylesheet, so a UA rule the author never wrote can only be kept
  quiet by naming the element. `hr` is the single case that needs it today; a second one would be a
  reason to look for a real fix rather than to add another exemption.
- **A value nothing PARSES is a value nothing can report.** The audit that diffs what
  `StyleResolver` reads against what `UnsupportedCss` lists cannot see a syntax the resolver does
  not recognise — `calc()` was that case, falling through to the unparseable fallback with no
  diagnostic possible. Re-run the audit when adding properties, and remember it has this blind
  spot.

## Known limitations that are workarounds, not bugs

Each is documented in `CLAUDE.md`; they are listed here so that removing one is recognisable as
work rather than as tidying.

- **AngleSharp compares specificity across cascade origins**, where the specification resolves
  origin first. A consumer reset relying on `* { margin: 0 }` will not clear the UA margins on
  `body` and `p`. `Inputs/flatten.css` names elements explicitly to work around it. Fixing it
  properly means filtering declarations by origin, which `ComputeCascadedStyle` does not expose.
- **AngleSharp drops some declarations rather than passing the value through**, so they can be
  neither honoured nor reported: `revert`, `text-overflow`, the `min-content`/`max-content`/
  `fit-content` sizing keywords, `aspect-ratio` given a single number, `overflow-wrap: anywhere`,
  `content` given a `string()`, and `recto`/`verso` on both break spellings. The whole of `@page`
  except its margins is dropped too — its `size`, its selector, and its margin box at-rules — and
  is recovered by hand from the stylesheet's own text, because a page size is a whole-document
  difference and a running header is the reason most documents have the rule.
- **A generic font family cannot be pinned in the corpus**, because a generic name is not legal as
  an `@font-face` family, so "does `<pre>` default to monospace" is not measurable here.
- **`line-height: normal` imitates Chrome's rounding** rather than following a specification,
  because CSS explicitly leaves the value to the user agent. If the reference browser ever changes,
  this is the first thing that will move. The same is true of every number in `ListMarkers`.

## Infrastructure

- **The corpus references are generated on one platform.** Regenerating on the machine that
  produced them is known to be byte-identical, so the generator is at least deterministic — but
  that is the lesser half. The claim that matters is still untested: generating all 120 on a second
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
- **`inline/vertical_align`'s notes claim a residual it no longer has.** They record SSIM 0.9989;
  the scenario is pixel-identical now. It is the only scenario whose stated residual disagrees with
  what the corpus records, and correcting the prose is the whole of the work.
