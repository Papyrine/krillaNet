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

153 scenarios across 10 categories, 1994 element boxes matched. **Box geometry matches Chrome
exactly on every one** — worst offset 0.00px, worst size 0.00px, and nothing unmatched — and 106
read SSIM 1.0000, of which 74 are pixel-identical outright. The other 32 differ on a scattering of
antialiased pixels, which is what `AE` is there to show.

Forty-seven read below 1.0000. None is a mystery, and none should be "fixed" by regenerating a
baseline:

| Scenario | AE | SSIM | Cause |
| --- | --- | --- | --- |
| `page/tall_image` | 0.0122 | 0.9750 | Chromium's PRINTER drops a margin its own layout keeps, below |
| `block/background_repeat` | 0.0167 | 0.9842 | Chromium's PRINTER blurs a SPACED background's tile edges, below |
| `page/table_header` | 0.0049 | 0.9900 | Sub-pixel glyph positioning at 24 and 30px; page three is identical |
| `table/cell_baseline` | 0.0031 | 0.9926 | Chromium's PRINTER does not apply cell baseline alignment, below |
| `text/kerning` | 0.0201 | 0.9945 | Sub-pixel glyph positioning, below |
| `text/ligatures` | 0.0099 | 0.9982 | Same |
| `page/table_footer` | 0.0114 | 0.9984 | Same, at 24 and 30px; page three is identical |
| `block/shadows` | 0.0057 | 0.9985 | Antialiasing on `#rounded`'s corner; no pixel differs by more than 2 of 255 |
| `image/svg` | 0.0028 | 0.9988 | Sub-pixel glyph positioning in the `<text>` inside the picture |
| `block/border_radius_sides` | 0.0019 | 0.9988 | A corner seam where two colours hand over, below |
| `block/bevelled_borders` | 0.0009 | 0.9990 | Antialiasing where two colours meet on a mitre, below |
| `table/bevelled_borders` | 0.0005 | 0.9990 | The same mitres |
| `page/break_avoid` | 0.0045 | 0.9991 | Sub-pixel glyph positioning; pages one and three are identical |
| `page/fixed_repeat` | 0.0039 | 0.9992 | Sub-pixel glyph positioning |
| `block/border_styles` | 0.0002 | 0.9995 | A vertical dotted edge, below |
| `page/break_between_lines` | 0.0017 | 0.9996 | Sub-pixel glyph positioning |
| `page/trailing_margin` | 0.0024 | 0.9996 | Same |
| `block/counters` | 0.0013 | 0.9997 | Same |
| `page/float_break` | 0.0023 | 0.9997 | Same |
| `text/underline_offset` | 0.0002 | 0.9997 | Glyph edges |
| `block/min_width` | 0.0009 | 0.9998 | Sub-pixel glyph positioning |
| `page/break_inside` | 0.0012 | 0.9998 | Same, on page two; page one is identical |
| `page/multi_page_flow` | 0.0016 | 0.9998 | Same |
| `page/orphans_widows` | 0.0019 | 0.9998 | Same; pages one and three are identical |
| `text/align_last` | 0.0013 | 0.9998 | Same |
| `text/word_spacing` | 0.0009 | 0.9998 | Same, across the widened spaces |
| `block/box_sizing` | 0.0004 | 0.9999 | Same, on the one line beside a float |
| `block/list_image` | 0.0006 | 0.9999 | Same |
| `block/outline` | 0.0006 | 0.9999 | Same |
| `float/overflow_bfc` | 0.0005 | 0.9999 | Same |
| `image/inline_flow` | 0.0006 | 0.9999 | One antialiased pixel column at an image edge |
| `inline/gradient` | 0.0111 | 0.9999 | Ramp quantisation, plus glyph edges |
| `inline/nowrap` | 0.0010 | 0.9999 | Sub-pixel glyph positioning |
| `inline/text_indent` | 0.0005 | 0.9999 | Same |
| `link/fragment` | 0.0001 | 0.9999 | Same |
| `link/wrapped` | 0.0000 | 0.9999 | Same |
| `page/tall_block` | 0.0004 | 0.9999 | Same |
| `position/absolute` | 0.0005 | 0.9999 | One pixel column where two backgrounds meet at a fractional edge |
| `text/decoration_style` | 0.0002 | 0.9999 | `text-decoration-skip-ink`, deliberate, below |
| `text/font_size_keywords` | 0.0006 | 0.9999 | Sub-pixel glyph positioning |
| `text/letter_spacing` | 0.0005 | 0.9999 | Same |
| `text/text_transform` | 0.0002 | 0.9999 | Same |
| `text/word_break_opportunity` | 0.0002 | 0.9999 | Same |
| `ua/blockquote_pre` | 0.0005 | 0.9999 | Same |
| `ua/presentational` | 0.0016 | 0.9999 | Corner mitres, plus glyph edges on the right-aligned heading |
| `ua/presentational_text` | 0.0006 | 0.9999 | Bevelled corners, two antialiased bullets, and glyph edges |
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

Seven causes cover the rest. Four are property gaps with a fix behind them — a vertical dotted edge,
`text-decoration-skip-ink`, the gradient quantisation and a rounded corner where two colours hand
over — and each is written up in the sections below. The other three are general: **sub-pixel glyph
positioning**, **a box edge landing on a fractional position**, and **two antialiased edges meeting
on a mitre**, which is the same shortfall `PaintUniformBorder` avoids for a uniform border by
painting one ring.

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
- **Multi-column.** `column-count` and `column-width` are reported and lay out as one column.

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
What is left:

- **It is OFF by default.** Turning it on changes the bytes of every document, so it wants a round
  of its own — and it is the last thing here that is a decision rather than work.
- **A list item's marker is an artifact, so there is no `Lbl` beside the `LBody`.** A reader is told
  the item is an item and not what its bullet says, which is right for a disc and wrong for a
  counter. The marker is drawn by `ListMarkers` at the end of block layout, outside the span the
  item's own content is recorded under, so giving it a label means recording it against the item and
  splitting the two on the way out.
- **A table cell carries no `WithRowSpan`, `WithColumnSpan` or `WithHeaders`.** krilla takes all
  three and the grid knows all three, so this is plumbing rather than a question — but a spanning
  cell currently reads as an ordinary one, which is exactly the case a reader most needs told.
- **No ARIA is read.** A figure takes its `alt`, its `<title>` child or its `title` attribute, and
  nothing else anywhere takes anything: `aria-label`, `aria-describedby` and `role` are all ignored,
  and so is `abbr` on a header cell, which is the one PDF has a field for.

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
  `font-variant-caps` and `font-variant-ligatures`. Four more were dropped and are now recovered
  from the stylesheet's own text by `CssSource`: `string-set`, `page`, a `content` value carrying
  `string()`, and a `::before`'s `display`. The whole of `@page` except its margins goes the same
  way — its `size`, its selector, and its margin box at-rules.
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
