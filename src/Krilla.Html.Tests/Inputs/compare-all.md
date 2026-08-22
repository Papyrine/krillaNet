# All scenarios (73)

The browser reference (left) beside the page Krilla.Html produced (right). `AE` is the fraction of pixels that differ and `SSIM` is structural similarity; neither is asserted. The worst offset is the largest positional disagreement in CSS pixels between the rendered element geometry and the browser's, and is the number to watch — it reaches zero exactly when the layout is right.

<details>
<summary>Contents</summary>

- [block/anonymous](#block-anonymous)
- [block/auto_margins](#block-auto_margins)
- [block/auto_width](#block-auto_width)
- [block/background_color](#block-background_color)
- [block/borders](#block-borders)
- [block/box_model](#block-box_model)
- [block/box_sizing](#block-box_sizing)
- [block/margin_collapse](#block-margin_collapse)
- [block/margin_collapse_blocked](#block-margin_collapse_blocked)
- [block/margin_collapse_parent](#block-margin_collapse_parent)
- [block/max_width](#block-max_width)
- [block/min_height](#block-min_height)
- [block/min_width](#block-min_width)
- [block/nested_blocks](#block-nested_blocks)
- [block/overflow_paint](#block-overflow_paint)
- [block/percentage_width](#block-percentage_width)
- [float/basic](#float-basic)
- [float/clear](#float-clear)
- [float/margins](#float-margins)
- [float/mid_line](#float-mid_line)
- [float/shrink_to_fit](#float-shrink_to_fit)
- [float/stacking](#float-stacking)
- [image/block_centred](#image-block_centred)
- [image/data_uri](#image-data_uri)
- [image/inline_flow](#image-inline_flow)
- [image/intrinsic](#image-intrinsic)
- [image/max_width](#image-max_width)
- [image/sized](#image-sized)
- [inline/font_size_em](#inline-font_size_em)
- [inline/font_style](#inline-font_style)
- [inline/font_weight](#inline-font_weight)
- [inline/inline_block](#inline-inline_block)
- [inline/inline_block_sizing](#inline-inline_block_sizing)
- [inline/justify](#inline-justify)
- [inline/line_height](#inline-line_height)
- [inline/line_height_normal](#inline-line_height_normal)
- [inline/nested_inline](#inline-nested_inline)
- [inline/simple_text](#inline-simple_text)
- [inline/text_align](#inline-text_align)
- [inline/text_indent](#inline-text_indent)
- [inline/white_space](#inline-white_space)
- [inline/white_space_pre](#inline-white_space_pre)
- [inline/wrapping](#inline-wrapping)
- [link/external](#link-external)
- [link/fragment](#link-fragment)
- [link/wrapped](#link-wrapped)
- [page/absolute_break](#page-absolute_break)
- [page/break_between_lines](#page-break_between_lines)
- [page/float_break](#page-float_break)
- [page/multi_page_flow](#page-multi_page_flow)
- [page/page_size](#page-page_size)
- [page/table_break](#page-table_break)
- [page/tall_block](#page-tall_block)
- [position/absolute](#position-absolute)
- [position/anchors](#position-anchors)
- [position/fixed](#position-fixed)
- [position/relative](#position-relative)
- [table/anonymous](#table-anonymous)
- [table/auto_widths](#table-auto_widths)
- [table/empty](#table-empty)
- [table/fixed_layout](#table-fixed_layout)
- [table/sections](#table-sections)
- [table/spacing_borders](#table-spacing_borders)
- [table/spans](#table-spans)
- [text/kerning](#text-kerning)
- [text/ligatures](#text-ligatures)
- [ua/acid1](#ua-acid1)
- [ua/blockquote_pre](#ua-blockquote_pre)
- [ua/headings](#ua-headings)
- [ua/hr](#ua-hr)
- [ua/lists](#ua-lists)
- [ua/list_markers](#ua-list_markers)
- [ua/paragraphs](#ua-paragraphs)

</details>

## block/anonymous

# block/anonymous

A block container is all-block or all-inline. When a document mixes the two — a text node beside a
block sibling, which `BoxBuilder` calls the formatting of every readable HTML document — the stray
inline content is wrapped in an anonymous block so ordering is preserved.

Nothing in the corpus contained that arrangement. Every container was one or the other:
`ua/acid1`'s `<li>` holds a bare `<p>`, and the text that looks adjacent to a block elsewhere is
collapsible whitespace that generates no box at all.

The anonymous boxes are not elements, so they do not appear in `getBoundingClientRect()` and the
geometry comparison sees only `#mixed` and `#inner`. The pixels are what measure them: the two
runs of text have to sit above and below the block child, in source order, at the line positions
an anonymous block would give them.

It found a defect immediately. `BoxBuilder` gathered ALL of a container's stray inline content
into a SINGLE anonymous block and inserted it at index 0, which is right only while the mixed case
is leading text before the first block child: the trailing run was hoisted above `#inner` along
with the leading one, putting it at y=120 where Chrome puts it at y=72. The container's height was
identical either way, which is why nothing else in the corpus could have shown it.

`BoxBuilder.CloseRun` now closes one anonymous block per contiguous run, appended in source order,
and the index bookkeeping that used to shift every float and positioned box by one went with it —
a run closes only at a block-level sibling, and an out-of-flow box declared inside a run belongs at
the top of the block that run becomes, which is the count it was already recorded against.

What to look at when it moves: `#inner` back at y=120 is the single-block behaviour returning.
Text overlapping `#inner` is an anonymous block contributing no height. `#mixed` taller or shorter
than 144px is a line count changing, which is a different bug from this one.

**Boxes**: 4 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0002 · SSIM 1.0000** |
| <img src="block/anonymous/reference_0001.png" width="480"> | <img src="block/anonymous/result%23page_0001.verified.png" width="480"> |


## block/auto_margins

Two auto margins centre; one auto margin absorbs. The first box sits at x=208, the second flush to
the right edge.

**Boxes**: 4 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="block/auto_margins/reference_0001.png" width="480"> | <img src="block/auto_margins/result%23page_0001.verified.png" width="480"> |


## block/auto_width

An auto width takes whatever the margins leave. The first box fills the page; the second is
816 - 100 - 250 = 466 wide.

**Boxes**: 4 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="block/auto_width/reference_0001.png" width="480"> | <img src="block/auto_width/result%23page_0001.verified.png" width="480"> |


## block/background_color

A background paints the border box, and a child paints over its parent. Flat fills with hard edges,
so this scenario should reach near-identical pixels. If it does not, the units or the DPI are
misaligned and no text scenario's numbers can be trusted yet.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="block/background_color/reference_0001.png" width="480"> | <img src="block/background_color/result%23page_0001.verified.png" width="480"> |


## block/borders

Four edges, four widths, four colours. Deliberately mismatched: with a uniform border a corner
mitre is invisible, and this renderer paints corners as overlapping rectangles rather than mitring
them. The corners are where that difference shows.

**Boxes**: 3 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0001 · SSIM 1.0000** |
| <img src="block/borders/reference_0001.png" width="480"> | <img src="block/borders/result%23page_0001.verified.png" width="480"> |


## block/box_model

The box model in one box. The border box is 200+40+10 wide and 100+20+10 tall; the margin moves it
without growing it. Everything downstream assumes this is right, so it is the first thing to check
when a whole category goes wrong at once.

**Boxes**: 3 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="block/box_model/reference_0001.png" width="480"> | <img src="block/box_model/result%23page_0001.verified.png" width="480"> |


## block/box_sizing

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1** _(no page)_ |
| <img src="block/box_sizing/reference_0001.png" width="480"> |  |


## block/margin_collapse

Adjacent siblings. The 30px below the first box meets the 50px above the second and collapses to
50, not 80. The first box's own top margin collapses out through body and html, so it starts at 30
rather than 0.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="block/margin_collapse/reference_0001.png" width="480"> | <img src="block/margin_collapse/result%23page_0001.verified.png" width="480"> |


## block/margin_collapse_blocked

The other half of margin_collapse_parent. A single pixel of top padding and a bottom border are
enough to stop the collapse in both directions, so both inner margins stay inside and the outer box
grows by 80px. The pair is what proves the rule rather than a coincidence.

**Boxes**: 4 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="block/margin_collapse_blocked/reference_0001.png" width="480"> | <img src="block/margin_collapse_blocked/result%23page_0001.verified.png" width="480"> |


## block/margin_collapse_parent

Collapse through a parent with no border or padding to stop it. The inner top margin escapes
through the outer top edge, so the outer box starts 40px down and does NOT include that margin in
its own height. The bottom margin escapes the same way.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="block/margin_collapse_parent/reference_0001.png" width="480"> | <img src="block/margin_collapse_parent/result%23page_0001.verified.png" width="480"> |


## block/max_width

max-width with auto margins, the commonest centring idiom there is. It only works because CSS
re-runs the width algorithm once max-width has clamped an auto width, handing the leftover space
back to the margins. A naive clamp leaves the box at the left edge.

**Boxes**: 3 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="block/max_width/reference_0001.png" width="480"> | <img src="block/max_width/result%23page_0001.verified.png" width="480"> |


## block/min_height

# block/min_height

`min-height` and `max-height` were read by nothing and reported by nothing, so a box carrying
either was laid out at its content height and the document was silently wrong below it. They now
resolve alongside the width pair they mirror, and this measures all three ways they can fail.

- **`#held`** — one line of content in a box with an 80px minimum. The simplest case, and the one
  that survives if the property is read and then dropped.
- **`#cut`** — four lines in a box with a 40px maximum. Nothing is clipped: `overflow` is not
  implemented and is reported, so the content runs past the bottom edge and over what follows,
  which is exactly what `overflow: visible` asks for. A box that swallowed its overflow would look
  tidier and be wrong. That the overflowing text stays visible over the next box is a separate
  property, and `block/overflow_paint` is where it is measured.
- **`#both`** — a minimum taller than the maximum. `ClampHeight` applies the maximum first, so the
  minimum wins at 90px, the same order `Clamp` uses horizontally.

Percentages are deliberately absent: a percentage height resolves against a containing height that
is indefinite throughout a paginated document, and CSS says such a percentage behaves as though it
were not there — the same rule `height` itself follows here.

What to look at when it moves: `#held` at one line tall is `min-height` unread. `#cut` at four
lines is `max-height` unread, and `#cut` with its text cut off at 40px is overflow being clipped
where nothing should clip it. `#both` at 30px is the two clamps in the wrong order.

**Boxes**: 9 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0002 · SSIM 1.0000** |
| <img src="block/min_height/reference_0001.png" width="480"> | <img src="block/min_height/result%23page_0001.verified.png" width="480"> |


## block/min_width

# block/min_width

`min-width` is implemented, with an explicit precedence rule — applied after `max-width`, so a
minimum wider than the maximum wins, which is the order CSS specifies — and it appeared in no
scenario at all. `block/max_width` measures only the other half of the pair.

Three arrangements, because the property fails in three different ways:

- **`#held`** — a declared width below the minimum. The simplest case, and the one that would
  survive if `min-width` were read and then dropped.
- **`#both`** — a minimum wider than the maximum. This is the one the precedence rule exists for:
  clamping to the maximum last gives 100px, which looks correct until it is compared.
- **`#relative`** — a percentage, resolved against the containing block rather than the viewport,
  which is the distinction `StyleResolver` reads the cascaded style for.

What to look at when it moves: `#held` at 50px is `min-width` unread. `#both` at 100px is the two
clamps applied in the wrong order, and at 300px is neither applied. `#relative` at any width other
than 240px is a percentage resolved against the wrong box.

**Boxes**: 8 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0009 · SSIM 0.9998** |
| <img src="block/min_width/reference_0001.png" width="480"> | <img src="block/min_width/result%23page_0001.verified.png" width="480"> |


## block/nested_blocks

Containing widths through three levels. Each auto width is its parent content width, so an error in
the border or padding subtraction compounds visibly rather than staying hidden at one level.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="block/nested_blocks/reference_0001.png" width="480"> | <img src="block/nested_blocks/result%23page_0001.verified.png" width="480"> |


## block/overflow_paint

# block/overflow_paint

CSS 2.1 Appendix E is a set of phases over a whole stacking context, not an order within one box:
every in-flow block background and border goes down in tree order (step 3), then the floats (step
4), then all the inline content (step 7). Applying the same sequence box by box instead is
indistinguishable while nothing overlaps, and this is what it looks like when something does.

- **`#cut`** overflows its own box by three lines, and those lines land on `#under`. Painted per
  box, `#under`'s background arrives after them and hides them; painted in phases, the text is on
  top, which is what a browser does.
- **`#hang`** is a float taller than the box that declared it, and a block does not grow to
  contain its floats — so it hangs over `#below` entirely. Same defect, reached the other way: a
  float belongs to the layer rather than to the box that declared it, so it goes down after every
  background in the layer and before every line.

Nothing else in the corpus overlaps two boxes without positioning one of them, which is why the
whole restructure it measures is invisible everywhere else: all 69 other scenarios came out
pixel-identical across it, and only the order of the operators in their PDFs changed.

What to look at when it moves: either coloured block covering what should be over it. `#cut`'s
last three lines disappearing is the background phase losing its reach; `#hang` disappearing is the
float phase losing its.

**Boxes**: 8 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0003 · SSIM 1.0000** |
| <img src="block/overflow_paint/reference_0001.png" width="480"> | <img src="block/overflow_paint/result%23page_0001.verified.png" width="480"> |


## block/percentage_width

Percentages resolve against the containing block CONTENT width, which is 600 here and not 640.
Percentage margins resolve against that same width, not against the height.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="block/percentage_width/reference_0001.png" width="480"> | <img src="block/percentage_width/result%23page_0001.verified.png" width="480"> |


## float/basic

What a float does to the text beside it, and what it deliberately does not do to the boxes.

Three things are measured here, and the third is the one that catches an implementation out:

- `#left` and `#right` shorten the line boxes of the paragraph beside them. The paragraph's own
  border box is unchanged and still spans the full 400px — a fact readily missed, because the text
  inside it moves and the box does not.
- `#tall` is taller than the single line beside it, so it hangs out of the bottom of `#wrap3`. A
  block does not grow to contain a float, and a document relying on that will overlap whatever
  follows. That is correct rather than a defect, and `#wrap3` records how far it overflows.
- `#block` sits beside the same float and is NOT moved or narrowed. CSS shortens line boxes, not
  block boxes, so an ordinary block overlaps the float. An implementation that narrowed the
  block instead would look plausible on this page and be wrong everywhere.

The backgrounds are what makes the difference between the second and third points visible in the
pixels rather than only in the geometry.

**Boxes**: 12 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0001 · SSIM 1.0000** |
| <img src="float/basic/reference_0001.png" width="480"> | <img src="float/basic/result%23page_0001.verified.png" width="480"> |


## float/clear

`clear`, in the three forms that behave differently.

- `#sides` has floats of different heights on either side. `#cl` clears only the left one, so it
  drops to 40px and still has the right float beside it, shortening its line. `#cr` clears the
  right one, which is lower, so it drops further. Treating `clear` as "below every float" would put
  both at the same place and pass a scenario with only one float in it.
- `#chain` puts `clear: left` on a FLOAT. `#f2` has room beside `#f1` and descends anyway, because
  clearance applies before the sideways search rather than instead of it.
- `#margins` clears a float whose bottom margin is larger than its height. Clearance measures to the
  margin box, so `#cm` lands 40px below the visible box rather than against it — which is also the
  check that the float context stores margin boxes rather than border boxes.

What is deliberately NOT measured here is clearance interacting with a large collapsed margin.
CSS 2.1 §9.5.2 makes clearance a separate quantity that also stops the margin collapsing through,
and the engine applies clearance after the collapsed margin instead. The difference appears only
when the cleared box has a margin big enough to clear the float unaided; `src/todo.md` records it.

**Boxes**: 14 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0002 · SSIM 1.0000** |
| <img src="float/clear/reference_0001.png" width="480"> | <img src="float/clear/result%23page_0001.verified.png" width="480"> |


## float/margins

# float/margins

Every float rule is stated against the MARGIN box, and no other float scenario has a float with
margins on it — `float/basic` has none at all, and `float/clear`'s `#m` carries a bottom margin
only, to measure clearance. So the three places the margin box matters were unmeasured in pixels:

- **`#spaced`** — line shortening. The paragraph's lines start 20px past the float's border edge,
  and the float sits 12px in from the container's left edge rather than against it.
- **`#low`** — the flow position. A float with `margin-top: 24px` puts its MARGIN box at the flow
  position, so the border box lands 24px down. Adding the margin when choosing the position and
  again when translating the box into place lands it at 48px, which is a bug that was fixed once
  and that nothing in the corpus could have caught.
- **`#a`/`#b`** — float-against-float placement. Two 170px floats fit side by side in 400px; two
  210px margin boxes do not, so the second descends.

All three are exact, and the page reads SSIM 1.0000.

What to look at when it moves: a horizontal shift of exactly one margin on `#spaced`'s text is
line shortening reverting to the border box. `#low` at y=48 rather than y=24 is the double
application. `#b` beside `#a` rather than below it is the fit test reading border boxes.

**Boxes**: 12 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0001 · SSIM 1.0000** |
| <img src="float/margins/reference_0001.png" width="480"> | <img src="float/margins/result%23page_0001.verified.png" width="480"> |


## float/mid_line

# float/mid_line

A line is shortened by the floats its box OVERLAPS, not by those under its top edge. The two
readings agree on every arrangement where floats begin and end on line boundaries, which is nearly
all of them, and this is the arrangement where they do not.

`#early` occupies y=0..10 and is 60 wide. `#late` clears it, so it starts at y=10 — inside the
first line box, which runs y=0..24 — and it is 220 wide. The first line therefore has 180px to
work with, from x=220 to the container's right edge. Sampling the band at the line's top edge
alone sees only `#early` and reports 340px, which fits roughly twice as many words.

This is the case `FloatGeometryTests` keeps as `e1`, which is the only one of its nineteen
arrangements that distinguishes the two readings. It had no pixel measurement until now: this
scenario is that case as a rendered page, so the word count on the first line reports it directly.

Measured, the page is pixel-identical to the reference: the band reading is right, and this is
now the confirmation of it in pixels rather than in a probe case.

What to look at when it moves: the first line carrying more words than the second is the top-edge
reading. The lines below the float are unaffected either way, which is what makes the first line
the whole signal.

**Boxes**: 6 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="float/mid_line/reference_0001.png" width="480"> | <img src="float/mid_line/result%23page_0001.verified.png" width="480"> |


## float/shrink_to_fit

How wide a float with no declared width becomes, which CSS 2.1 §10.3.5 gives as
`min(max(min-content, available), max-content)`.

The three terms of that formula each need a case to be distinguishable, and this has one for each:

- `#short` is narrower than the container, so it takes its max-content width — the width of its
  text with no wrapping at all. This measures the text advance itself, not merely the algorithm.
- `#long` wants more than 400px, so it takes what is available and wraps inside itself. An
  implementation using max-content unconditionally overflows here.
- `#word` is a single unbreakable word inside a 120px container. Neither term of the outer minimum
  can go below min-content, so the float overflows its parent rather than breaking the word.
- `#overflow` is wider than the container outright, so it hangs out to the right AND leaves the
  paragraph beside it no band at all. That paragraph descends below the float instead of drawing
  its line in zero width, which is the CSS 2.1 §9.5 rule for a line box shortened to nothing.

The last is worth the scenario on its own: a shortened line and a line with nowhere to go are
different code paths, and only one of them is exercised by every other case here.

**Boxes**: 14 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 0.9999** |
| <img src="float/shrink_to_fit/reference_0001.png" width="480"> | <img src="float/shrink_to_fit/result%23page_0001.verified.png" width="480"> |


## float/stacking

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

**Boxes**: 18 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="float/stacking/reference_0001.png" width="480"> | <img src="float/stacking/result%23page_0001.verified.png" width="480"> |


## image/block_centred

A block-level image centred by auto margins. The width comes from the image rather than from the
container, and the leftover space is then split between the margins exactly as it would be for a
div of the same width.

**Boxes**: 3 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="image/block_centred/reference_0001.png" width="480"> | <img src="image/block_centred/result%23page_0001.verified.png" width="480"> |


## image/data_uri

A data URI, the one image source that carries its own bytes and needs no file access at all. Sizing
is otherwise identical to a file source, so this measures the decoding path rather than the layout.

**Boxes**: 3 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="image/data_uri/reference_0001.png" width="480"> | <img src="image/data_uri/result%23page_0001.verified.png" width="480"> |


## image/inline_flow

An image is an atomic inline: it flows on the line like a word, breaks before or after but never
inside, and sits its bottom edge on the baseline. Because it is 32px tall in a 24px line, it pushes
the line's top upward rather than growing it downward, which is what baseline alignment means for a
replaced element.

**Boxes**: 4 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0007 · SSIM 0.9998** |
| <img src="image/inline_flow/reference_0001.png" width="480"> | <img src="image/inline_flow/result%23page_0001.verified.png" width="480"> |


## image/intrinsic

An image with no width or height takes its intrinsic size: 64x32 CSS pixels, one per image pixel.
The wrapper exists so the image's own box is visible against a background rather than against the
page.

**Boxes**: 4 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="image/intrinsic/reference_0001.png" width="480"> | <img src="image/intrinsic/result%23page_0001.verified.png" width="480"> |


## image/max_width

max-width clamping with the height left auto. The declared 600px width is clamped to the 150px
container, and the height must be rescaled by the same factor to 75px. Skipping that rescale is how
images end up distorted inside responsive containers, so this scenario exists to catch it.

**Boxes**: 4 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="image/max_width/reference_0001.png" width="480"> | <img src="image/max_width/result%23page_0001.verified.png" width="480"> |


## image/sized

The three sizing paths. Width alone gives 192x96 from the 2:1 ratio; height alone gives 192x96 the
other way; both given wins over the ratio and the image is deliberately distorted to 150x150. The
width and height content attributes are presentational hints, which AngleSharp does not surface as
declarations, so they are applied after the cascade.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="image/sized/reference_0001.png" width="480"> | <img src="image/sized/result%23page_0001.verified.png" width="480"> |


## inline/font_size_em

The em trap. A relative font-size resolves against the PARENT size, while every other em in the
same rule resolves against the size being computed. So the inner box is 16px with 8px of padding,
not 16px with 10px. A unitless line-height multiplies each element's own size.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="inline/font_size_em/reference_0001.png" width="480"> | <img src="inline/font_size_em/result%23page_0001.verified.png" width="480"> |


## inline/font_style

Italic resolves to the italic file. Style is matched before weight in CSS font matching, because a
wrong slant reads as a different face while a wrong weight reads as the same face rendered lighter.

**Boxes**: 4 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="inline/font_style/reference_0001.png" width="480"> | <img src="inline/font_style/result%23page_0001.verified.png" width="480"> |


## inline/font_weight

Weight selection resolves to a different file, not a synthesised emboldening. Liberation is
metric-compatible with Arial, so many individual glyphs have identical advances in both weights and
the difference only accumulates over a sentence.

**Boxes**: 4 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="inline/font_weight/reference_0001.png" width="480"> | <img src="inline/font_weight/result%23page_0001.verified.png" width="480"> |


## inline/inline_block

# inline/inline_block

`display: inline-block` used to lay out as a block and report a diagnostic, so a row of anything
built out of it became a column. It is now an atomic inline: one unbreakable box on a line, with
contents laid out as a block in a formatting context of its own.

What separates it from the other atomic inline the engine already had is the baseline, and three of
these four arrangements are about that.

- **`#baseline`** — one line of content. Its baseline is its own line's, so the text inside it and
  the text beside it sit on the same baseline. An image would put its bottom edge there instead.
- **`#two`** — two lines of content. CSS 2.1 §10.8.1 takes the LAST line's baseline, so the box
  hangs upward and the text beside it lines up with its second line. Taking the first line's
  instead is the plausible reading, and puts the box a whole line too low.
- **`#empty`** — no in-flow line box at all, which is where the rule falls back to the bottom
  MARGIN edge. That is the image case, reached from the other direction.
- **`#model`** — padding, border and margins on all four sides. The horizontal margins hold the
  text away exactly as the border box does, which is why the token measures the margin box rather
  than the border box; the vertical ones do not collapse with anything, the box establishing its
  own formatting context.

What to look at when it moves: any of the four boxes on a line of its own is the atomic-inline
path being lost entirely. `#two` a line too low is the first-line baseline. `#baseline` sitting a
few pixels low with its text still aligned is half-leading, not this.

**Boxes**: 10 matched, worst offset 0.00px, worst size 0.00px.

Not rendered: `html > body:nth-child(2) > p:nth-child(2) > span:nth-child(1) > br:nth-child(1)`

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0002 · SSIM 0.9999** |
| <img src="inline/inline_block/reference_0001.png" width="480"> | <img src="inline/inline_block/result%23page_0001.verified.png" width="480"> |


## inline/inline_block_sizing

# inline/inline_block_sizing

The other half of `inline/inline_block`: how wide an atomic inline gets, and what a line does with
a row of them.

- **`#fits`** — two boxes written with no white space between the tags, so there is no space
  between them on the page either. Each is shrink-to-fit at its max-content width.
- **`#spaced`** — the same two with a newline between the tags, which collapses to one space and
  separates them. It is the difference behind every `font-size: 0` hack in the wild, and it is
  measurable rather than a curiosity.
- **`#wraps`** — three 90px boxes with 8px margins. 294 fits and 392 does not, so the third moves
  to a second line: a line breaks before or after an atomic inline and never inside it.
- **`#squeezed`** — content wider than the container. Shrink-to-fit is
  `min(max(min-content, available), max-content)`, the same rule `BlockLayout.ShrinkToFit` gives a
  float, so the box takes the whole 300px and wraps inside itself rather than overflowing.

What to look at when it moves: `#fits` and `#spaced` rendering identically means white space
between the boxes is being dropped or invented. `#wraps` on one line means the box is being
measured to its border box rather than its margin box. `#squeezed` overflowing means shrink-to-fit
took the max-content width without clamping to what was available.

**Boxes**: 14 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0002 · SSIM 0.9999** |
| <img src="inline/inline_block_sizing/reference_0001.png" width="480"> | <img src="inline/inline_block_sizing/result%23page_0001.verified.png" width="480"> |


## inline/justify

Justification stretches the spaces on every line but the last. Distributing the slack evenly across
gaps is the simplest of several defensible rules, so some disagreement with the browser here is
expected and informative rather than a defect to chase.

**Boxes**: 3 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0001 · SSIM 1.0000** |
| <img src="inline/justify/reference_0001.png" width="480"> | <img src="inline/justify/result%23page_0001.verified.png" width="480"> |


## inline/line_height

Half-leading in both directions. The extra space of a large line-height is split evenly above and
below the text rather than hung underneath, and a line-height smaller than the natural height makes
lines overlap rather than clip.

**Boxes**: 4 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="inline/line_height/reference_0001.png" width="480"> | <img src="inline/line_height/result%23page_0001.verified.png" width="480"> |


## inline/line_height_normal

Probes what `normal` resolves to, which no stylesheet states: it comes from the font ascent,
descent and line gap, and from whether OS/2 asks for its typographic metrics to win over hhea.
Deliberately isolated, because every other text scenario sets line-height explicitly so that this
one question cannot contaminate them.

**Boxes**: 3 matched, worst offset 0.00px, worst size 0.00px.

Not rendered: `html > body:nth-child(2) > p:nth-child(1) > br:nth-child(1)`, `html > body:nth-child(2) > p:nth-child(1) > br:nth-child(2)`

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="inline/line_height_normal/reference_0001.png" width="480"> | <img src="inline/line_height_normal/result%23page_0001.verified.png" width="480"> |


## inline/nested_inline

Runs of different faces on one line. A line takes its height from the tallest inline box on it and
its baseline from the deepest, so a mismatched face changes both the wrap points and the line
positions. Note the explicit b and i rules: the shared reset flattens the UA stylesheet, so these
elements carry no styling of their own until a scenario gives them some.

**Boxes**: 3 matched, worst offset 0.00px, worst size 0.00px.

Not rendered: `html > body:nth-child(2) > p:nth-child(1) > b:nth-child(1)`, `html > body:nth-child(2) > p:nth-child(1) > i:nth-child(2)`, `html > body:nth-child(2) > p:nth-child(1) > b:nth-child(3)`, `html > body:nth-child(2) > p:nth-child(1) > b:nth-child(3) > i:nth-child(1)`

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="inline/nested_inline/reference_0001.png" width="480"> | <img src="inline/nested_inline/result%23page_0001.verified.png" width="480"> |


## inline/simple_text

One line, one font, one explicit line-height. The narrowest possible test of text measurement: if
the advances are wrong this line is the wrong width, and every other scenario in the category
inherits the error.

**Boxes**: 3 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="inline/simple_text/reference_0001.png" width="480"> | <img src="inline/simple_text/result%23page_0001.verified.png" width="480"> |


## inline/text_align

Alignment moves runs within the line box without changing the paragraph geometry, so the boxes here
should match exactly while the pixels carry the whole signal. A scenario where the two metrics
deliberately disagree about what they can see.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="inline/text_align/reference_0001.png" width="480"> | <img src="inline/text_align/result%23page_0001.verified.png" width="480"> |


## inline/text_indent

# inline/text_indent

`text-indent` was read by nothing and reported by nothing, so every indented paragraph in a
document laid out flush and the difference was invisible to the sink. It is now applied to the
first line of a block container, which is the only line it applies to.

Three arrangements, because the property has three separable behaviours:

- **`#prose`** — declared on the container and inherited. Both paragraphs are indented, and the
  second line of each is not. Applying it where it is declared rather than where the line is
  generated indents the first paragraph only, which is the failure `body { text-indent: 2em }`
  would produce in a real document.
- **`#hanging`** — a negative value, which puts the first line outside the content box. The
  container carries a left margin so the hang has somewhere to go.
- **`#centred`** — an indent NARROWS the band rather than shifting it, so the centred first line
  is centred in what is left and not merely pushed right by the whole indent.

The intrinsic widths carry it too, which nothing here measures: a table cell or a shrink-to-fit
float would otherwise be sized without room for the indent and wrap a line that was supposed to
fit.

What to look at when it moves: the second paragraph of `#prose` flush with the first line's edge
is inheritance lost. Every line indented is the property being applied per line rather than per
block. `#centred`'s first line pushed right by the full 60px is the indent shifting the band
instead of narrowing it.

**Boxes**: 9 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0005 · SSIM 0.9999** |
| <img src="inline/text_indent/reference_0001.png" width="480"> | <img src="inline/text_indent/result%23page_0001.verified.png" width="480"> |


## inline/white_space

# inline/white_space

`white-space` has five values and the engine implements all of them. `inline/white_space_pre`
measures two — `pre` and `normal` — and the other three had no coverage anywhere, in the corpus or
in a unit test.

Each of the three differs from `pre` in exactly one respect, which is what makes them worth
separating:

- **`pre-wrap`** preserves the spaces and the newline like `pre` does, but also breaks a line that
  runs out of room. Under `pre` the same text would overflow.
- **`pre-line`** collapses the runs of spaces like `normal` does, and keeps the newline. The
  leading indentation on each source line disappears; the break between them does not.
- **`nowrap`** collapses like `normal` and never breaks, so the text runs past the box's right
  edge rather than wrapping at it.

Monospace at 14px, so a column of characters is countable against the reference rather than merely
comparable.

What to look at when it moves: `#wrap` losing its interior spaces is `pre-wrap` being read as
`normal`; `#wrap` overflowing is it being read as `pre`. `#line` keeping its indentation is
`pre-line` preserving what it should collapse, and `#line` on one line is the newline being
dropped. `#nowrap` wrapping at 300px is the whole value being ignored.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0002 · SSIM 1.0000** |
| <img src="inline/white_space/reference_0001.png" width="480"> | <img src="inline/white_space/result%23page_0001.verified.png" width="480"> |


## inline/white_space_pre

The two halves of white-space processing side by side: preserved above, collapsed below. The pair
matters because collapsing is what stops indented markup from indenting the page, and getting the
phase order wrong only shows up when a newline has spaces around it, which is what all indented
markup is.

**Boxes**: 4 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="inline/white_space_pre/reference_0001.png" width="480"> | <img src="inline/white_space_pre/result%23page_0001.verified.png" width="480"> |


## inline/wrapping

Greedy line breaking at spaces. Where each break lands is decided by accumulated advances, so a
measurement error of a fraction of a pixel per glyph eventually moves a word to the next line and
changes the paragraph height. The box comparison catches that far more sharply than the pixel one.

**Boxes**: 3 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="inline/wrapping/reference_0001.png" width="480"> | <img src="inline/wrapping/result%23page_0001.verified.png" width="480"> |


## link/external

Two external links. Neither the pixel nor the box comparison can see a link annotation — it paints
nothing and is not an element box — so the annotations are read back out of the PDF and recorded in
the snapshot instead. Those two metrics staying at zero is the separate check that adding links
disturbed no layout.

The rectangle covers the text's em box rather than the whole line, so a generous line-height does
not make blank space clickable.

**Boxes**: 4 matched, worst offset 0.00px, worst size 0.00px.

Not rendered: `html > body:nth-child(2) > p:nth-child(1) > a:nth-child(1)`, `html > body:nth-child(2) > p:nth-child(2) > a:nth-child(1)`

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0003 · SSIM 1.0000** |
| <img src="link/external/reference_0001.png" width="480"> | <img src="link/external/result%23page_0001.verified.png" width="480"> |


## link/fragment

An internal link, and a broken one. The filler pushes the target onto the second page, so resolving
the fragment has to happen after pagination — a fragment names an element while a PDF internal link
names a page and a point on it.

The second anchor points at an id no element carries. It produces no annotation at all, rather than
one aimed at page one: a link that silently goes somewhere wrong is worse than a link that is not
there. Expect exactly one annotation.

**Boxes**: 6 matched, worst offset 0.00px, worst size 0.00px.

Not rendered: `html > body:nth-child(2) > p:nth-child(1) > a:nth-child(1)`, `html > body:nth-child(2) > p:nth-child(1) > a:nth-child(2)`

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0001 · SSIM 0.9999** |
| <img src="link/fragment/reference_0001.png" width="480"> | <img src="link/fragment/result%23page_0001.verified.png" width="480"> |
| **Page 2** | **Page 2. AE 0.0000 · SSIM 1.0000** |
| <img src="link/fragment/reference_0002.png" width="480"> | <img src="link/fragment/result%23page_0002.verified.png" width="480"> |


## link/wrapped

An anchor spanning several lines. A PDF link is a rectangle, so one that wraps needs one annotation
per line fragment rather than a single box around the lot — a single box would make the blank space
at the end of each line clickable, and on a centred or short line would cover text that is not part
of the link at all.

Expect one annotation per line the anchor touches, each covering only its own fragment.

**Boxes**: 3 matched, worst offset 0.00px, worst size 0.00px.

Not rendered: `html > body:nth-child(2) > p:nth-child(1) > a:nth-child(1)`

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 0.9999** |
| <img src="link/wrapped/reference_0001.png" width="480"> | <img src="link/wrapped/result%23page_0001.verified.png" width="480"> |


## page/absolute_break

# page/absolute_break

An absolute box is painted from the root rather than where it was declared, and it is placed in
continuous coordinates by a second pass that runs after flow and knows nothing about pages. Which
page it lands on is therefore decided last, by the painter, from a position computed by neither of
the two stages that produced it.

`#pinned` is declared at the top of a frame that begins on page one and offset to y=1150, which is
past the 1056 boundary. It belongs on page two, 94px down, and appears on page one not at all. The
frame's own line of text is what keeps page one from being a single flat colour, which
`BaselineHealthTests` reads as a page that rendered nothing.

What to look at: the box on page one is the hoisted absolute being assigned to its declaring
parent's page. The box missing entirely is it being clipped against the first page's box after
being assigned correctly — the same distinction `PdfPainter` draws between the page box and
`pageEnd`.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0003 · SSIM 1.0000** |
| <img src="page/absolute_break/reference_0001.png" width="480"> | <img src="page/absolute_break/result%23page_0001.verified.png" width="480"> |
| **Page 2** | **Page 2. AE 0.0000 · SSIM 1.0000** |
| <img src="page/absolute_break/reference_0002.png" width="480"> | <img src="page/absolute_break/result%23page_0002.verified.png" width="480"> |


## page/break_between_lines

A break landing inside a paragraph. The spacer leaves 76px of the first page, which fits two lines
of 32 but not three, so the third line must move to page two whole. A renderer that slices at the
page height instead would cut it in half.

**Boxes**: 4 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0017 · SSIM 0.9996** |
| <img src="page/break_between_lines/reference_0001.png" width="480"> | <img src="page/break_between_lines/result%23page_0001.verified.png" width="480"> |
| **Page 2** | **Page 2. AE 0.0000 · SSIM 1.0000** |
| <img src="page/break_between_lines/reference_0002.png" width="480"> | <img src="page/break_between_lines/result%23page_0002.verified.png" width="480"> |


## page/float_break

# page/float_break

A float is out of flow but not out of the page, and no scenario had one crossing a page boundary.
`#tall` runs 900..1200 with the boundary at 1056, so it has to be painted on both pages — its
upper 156px on the first and its lower 144px on the second — and the lines beside it have to keep
being shortened after the break.

The two halves of that are independent and can fail separately: the float's own box is painted by
`PdfPainter` per page, while the band the lines are laid against comes from `FloatContext` in
continuous coordinates, before pagination exists.

What to look at: the float's colour reaching the bottom edge of page one and resuming at the top
of page two, and the paragraph's first lines on page two still starting 120px in. Full-width lines
on page two are the band being lost at the break; a float painted on one page only is the box
being clipped to where it was declared.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0023 · SSIM 0.9997** |
| <img src="page/float_break/reference_0001.png" width="480"> | <img src="page/float_break/result%23page_0001.verified.png" width="480"> |
| **Page 2** | **Page 2. AE 0.0000 · SSIM 1.0000** |
| <img src="page/float_break/reference_0002.png" width="480"> | <img src="page/float_break/result%23page_0002.verified.png" width="480"> |


## page/multi_page_flow

Enough text to run past one page. Both engines break between lines rather than through them, which
is why the reference is printed rather than screenshotted and sliced: a sliced screenshot would cut
a line in half at every boundary and report a difference that came from how the reference was made
rather than from anything either engine did.

**Boxes**: 8 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0016 · SSIM 0.9998** |
| <img src="page/multi_page_flow/reference_0001.png" width="480"> | <img src="page/multi_page_flow/result%23page_0001.verified.png" width="480"> |
| **Page 2** | **Page 2. AE 0.0004 · SSIM 1.0000** |
| <img src="page/multi_page_flow/reference_0002.png" width="480"> | <img src="page/multi_page_flow/result%23page_0002.verified.png" width="480"> |


## page/page_size

A box that exactly fills one Letter page at 96 DPI: 816 x 1056 CSS pixels, here as 1040px of
content inside an 8px border. If this paginates to two pages the page height is off by a rounding
step somewhere, and every multi-page scenario will be wrong in the same way.

**Boxes**: 3 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="page/page_size/reference_0001.png" width="480"> | <img src="page/page_size/result%23page_0001.verified.png" width="480"> |


## page/table_break

# page/table_break

Every multi-page scenario in the corpus was plain paragraphs, so pagination had only ever been
measured against the one construct it was written for. A table is the first case where the break
candidates and the boxes disagree: `Paginator` breaks before a LINE that would straddle the
boundary, and a browser's printer moves a whole ROW.

Six 36px rows starting at y=950, so the table runs 950..1166 and the boundary at 1056 falls inside
the third row rather than between two of them. A break taken at the line inside that row splits
the row's background and border across the two pages; a break taken at the row moves the whole
thing overleaf.

It found two defects, and they compounded. The break went to the top of the LINE inside row
three rather than to the row's own top edge, six pixels lower, so everything on page two sat six
pixels high — 0.9659, the lowest SSIM the corpus has recorded. And the row it left behind still
painted its cell backgrounds down to the paper, stranding a sliver of the moved row at the foot of
page one.

`Paginator.Unbreakable` now treats a table row as one unit, the way a line is one everywhere else,
and `PdfPainter.PaintBox` culls a box whose top edge is at or after the break — which is a
different test from clipping at the break, and deliberately so: a box the break falls INSIDE is
fragmented, and a browser fills the rest of the page with that fragment. `page/multi_page_flow` is
the scenario that says so, and clipping cost it 1.6% of its pixels before the distinction was
drawn.

The geometry stays at zero throughout, because it is measured in continuous coordinates where
there is no break at all — which makes this a difference only the pixels could ever report.

What to look at: the vertical offset of page two's first row, and any part of it appearing at the
foot of page one. Six pixels of either is the line-based break returning.

**Boxes**: 23 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0001 · SSIM 0.9999** |
| <img src="page/table_break/reference_0001.png" width="480"> | <img src="page/table_break/result%23page_0001.verified.png" width="480"> |
| **Page 2** | **Page 2. AE 0.0003 · SSIM 0.9997** |
| <img src="page/table_break/reference_0002.png" width="480"> | <img src="page/table_break/result%23page_0002.verified.png" width="480"> |


## page/tall_block

# page/tall_block

A page break can be needed where no line offers one. `Paginator` breaks before a line that would
straddle the boundary, and a block taller than the page contains no lines — so there is no
candidate inside it, the break lands after the whole block, and everything between the page edge
and the block's end is never drawn. `NextTop` falls back to breaking at the page edge for exactly
this case, and nothing measured that fallback.

`page/page_size` stops one pixel short of reaching it: its content is 1040px plus a 16px border,
which is 1056 exactly — one page, with no fragment left over. Here the block runs 0..1400, so page
one is entirely filled by it, page two carries the remaining 344px, and the paragraph follows at
y=1400, which is 344 down page two.

`#mark` is a 40px block at the top, and it is there for `BaselineHealthTests`: without it page one
is a single flat colour, which is indistinguishable from a page that rendered nothing at all. It
generates no line, so the block it sits in stays free of break candidates and the fallback is
still what produces the break.

What to look at when it moves: a second page that is blank above the paragraph means the block was
not painted past the break. A single page means the fallback did not fire at all and the overflow
was dropped.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="page/tall_block/reference_0001.png" width="480"> | <img src="page/tall_block/result%23page_0001.verified.png" width="480"> |
| **Page 2** | **Page 2. AE 0.0004 · SSIM 0.9999** |
| <img src="page/tall_block/reference_0002.png" width="480"> | <img src="page/tall_block/result%23page_0002.verified.png" width="480"> |


## position/absolute

Absolute positioning against a nearest positioned ancestor.

`#frame` carries a 5px border and 10px padding so that the containing block is identifiable rather
than merely plausible. The containing block is the PADDING box: `#tl` at `top: 0; left: 0` lands
inside the border and outside the padding. Using the border box puts it 5px out in both axes and
using the content box puts it 10px in, and a frame with neither border nor padding cannot tell the
three apart — which is why this scenario has both.

- `#tl` and `#br` anchor to opposite corners, so the bottom-right one also measures that a `bottom`
  or `right` offset is applied after the box has been sized rather than before.
- `#stretch` gives both `left` and `right` with an auto width, the one arrangement where an
  absolute box fills its containing block instead of shrinking.
- `#fit` gives only `left`, so it shrinks to fit its content, the same rule a float follows.
- `#body` is the only in-flow content, so the frame's height comes from it alone. None of the four
  absolute boxes contributes to it, though two are taller than it — which `#after` records by
  sitting where the frame ends rather than where its contents do.

**Boxes**: 9 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0005 · SSIM 0.9999** |
| <img src="position/absolute/reference_0001.png" width="480"> | <img src="position/absolute/result%23page_0001.verified.png" width="480"> |


## position/anchors

Which box an absolute box is positioned against, in the three arrangements that differ.

- `#deep` is two levels down, inside a `#static` carrying a 30px margin, and anchors to `#outer` —
  skipping the static parent entirely. Its margin, its position and its padding contribute nothing.
  This is what makes `position: relative` on an outer element the standard way to anchor something
  nested, and an implementation that walked to the DOM parent would land 30px out in both axes.
- `#half` measures which dimension a percentage offset resolves against, and the answer is not one
  of them: `left` resolves against the containing block's WIDTH and `top` against its HEIGHT. A
  single square container cannot tell those apart, so `#pct` is deliberately not square.
- `#page` has no positioned ancestor at all, so its containing block is the page rather than the box
  that declares it. It is written inside a box most of the way down the page and lands near the top,
  which looks like a bug until the rule is known.

**Boxes**: 9 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="position/anchors/reference_0001.png" width="480"> | <img src="position/anchors/result%23page_0001.verified.png" width="480"> |


## position/fixed

# position/fixed

`position: fixed` is implemented, and reported as a diagnostic because its geometry is right
while only its paged-media behaviour is not: CSS says a fixed box repeats on every page, and
this places it on the one page its position falls on. Nothing checked the first half of that
claim.

One page, deliberately, so the repetition question does not arise and what is left is purely where
the box lands.

- **`#corner`** has no positioned ancestor, so both readings of "containing block" give the page
  and this measures the offsets alone, resolved against the page's edges.
- **`#inner`** is inside a `position: relative` ancestor. A fixed box's containing block is the
  page regardless, which is the single property that distinguishes it from an absolute box —
  `AbsoluteLayout` walks to the nearest positioned ancestor for both, so this arrangement is
  expected to sit at the frame's padding box plus the offsets rather than at the page's. That
  difference is the point of including it: it is a defect with a measurement rather than a comment.

It found the defect on its first render: `#inner` landed at (160, 500) rather than (40, 300),
out by exactly the frame's own margins, because `AbsoluteLayout` walked to the nearest positioned
ancestor for a fixed box as it does for an absolute one. It now carries the initial containing
block alongside the accumulated one and hands a fixed box the former. Both boxes are exact and the
page is pixel-identical.

This is still the only scenario in the corpus that reports a diagnostic, and `DiagnosticTests`
lists it by name as expected to. The report is about the construct rather than about this
document: on one page there is nothing for the repetition to do, and the reporter cannot know the
page count from the cascade.

What to look at when it moves: `#corner` shifting is the offsets or the page's own box. `#inner`
back at (160, 500) is the initial containing block being lost again on the way down the tree.

**Boxes**: 6 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="position/fixed/reference_0001.png" width="480"> | <img src="position/fixed/result%23page_0001.verified.png" width="480"> |


## position/relative

Relative positioning, which moves a box without moving anything else.

- `#flow` is the point of the whole value: `#b` shifts down and right, and `#c` sits exactly where
  it would have sat had `#b` never moved. The space `#b` was given stays given. An implementation
  that treated the offset as part of layout would push `#c` down and pass a scenario containing
  only one box.
- `#corners` covers the pair that reads backwards. `bottom` and `right` name the edge the box moves
  AWAY from, so `bottom: 8px` lifts a box and `right: 30px` pulls it left. Guessing the sign here is
  a coin toss and the corpus is the coin.
- `#nested` offsets a parent and measures the child, since the shift applies to the whole subtree
  rather than to one border box.

The heights are declared rather than derived so that a wrong offset shows as a moved box and not as
a reflow, which keeps the failure legible.

**Boxes**: 12 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0001 · SSIM 1.0000** |
| <img src="position/relative/reference_0001.png" width="480"> | <img src="position/relative/result%23page_0001.verified.png" width="480"> |


## table/anonymous

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

**Boxes**: 8 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0003 · SSIM 0.9996** |
| <img src="table/anonymous/reference_0001.png" width="480"> | <img src="table/anonymous/result%23page_0001.verified.png" width="480"> |


## table/auto_widths

The automatic column algorithm, which is where a table's layout is decided and where the CSS
specification stops being useful — 17.5.2 describes it as a sketch and leaves the distribution to
the user agent. Every number here was measured out of Chrome.

The four tables are the four regimes, and they are not variations on one rule:

- `#content` has no declared width, so it shrinks to fit and each column takes its max-content
  width.
- `#wide` is wider than its content wants, and the surplus goes to each column in proportion to its
  max-content width. Handing each column its maximum and giving the remainder to the last would
  also fill the row, and looks nothing like a browser.
- `#narrow` sits between the two intrinsic widths, and the rule changes: each column takes its
  min-content width plus a share of the slack proportional to how much it could grow. Applying the
  `#wide` rule here is visibly wrong.
- `#floor` declares a width narrower than the content can be broken to. The declaration loses — a
  table never renders narrower than the sum of its columns' min-content widths.

`#narrow` also measures the min-content computation itself, since the column widths depend on which
word in each cell is the longest.

**Boxes**: 25 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="table/auto_widths/reference_0001.png" width="480"> | <img src="table/auto_widths/result%23page_0001.verified.png" width="480"> |


## table/empty

# table/empty

An empty table occupies nothing — not two pixels square, which is what the edge border spacing
would give it if the spacing were added unconditionally. With no columns there is nothing for that
spacing to be outside of.

The paragraphs are the instrument. Under `flatten.css` they carry no margins, so they stack
tightly and any occupancy at all by a table between them shows up as a shift of everything below
it. Four paragraphs and three degenerate tables mean each table's contribution is isolated.

`#spaced` is the arrangement that distinguishes the two readings: a table with `border-spacing: 20px`
and no cells is 0x0, not 40x40.

All three occupy nothing, and the page was pixel-identical to the reference from the start. One
box was not: `#rowless`'s `<tbody>` sat at x=2, the default 2px edge spacing applied to a section
with no columns to be outside of, where the table's own width had known to skip it all along.
`PlaceRowBoxes` now applies the same rule. It was invisible — an empty section paints nothing — so
only the geometry comparison could ever have seen it, which is the case for that comparison
leading.

What to look at when it moves: any gap between two paragraphs. A 4px one is the default 2px edge
spacing being added twice; a 40px one is `#spaced`'s own declaration being added the same way. The
`tbody` back at x=2 is the section's edge spacing returning.

**Boxes**: 10 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="table/empty/reference_0001.png" width="480"> | <img src="table/empty/result%23page_0001.verified.png" width="480"> |


## table/fixed_layout

`table-layout: fixed` beside the automatic algorithm on the same markup, which is the only way to
see that it is a different algorithm rather than a tuning of one.

`#fixed` and `#auto` differ by one declaration and by nothing else. Fixed layout reads the first row
and stops: the pinned column takes its declared width and the two automatic columns split what is
left equally, whatever the second row contains. The automatic table reads every cell, so its second
row — the long one — decides the first column's width and the three columns come out unequal.

`#percent` measures a percentage column, which is where the two algorithms genuinely disagree about
what a percentage means. Under the automatic algorithm the percentage is the whole column, border
and padding included, because it has to compete with content widths that are measured that way.
Under fixed layout there is no content to compete with, so it is the cell's `width` under ordinary
content-box sizing and the padding is added on top. The difference is exactly the cell's padding,
which is small enough to look like a rounding error and is not one.

**Boxes**: 27 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="table/fixed_layout/reference_0001.png" width="480"> | <img src="table/fixed_layout/result%23page_0001.verified.png" width="480"> |


## table/sections

Row groups, captions and vertical alignment.

- `#ordered` writes its sections in the order tfoot, thead, tbody and expects them rendered as
  thead, tbody, tfoot. That reordering is the whole point of the elements — it lets a long table's
  markup put the footer next to the data it summarises — and a table that renders sections in
  source order looks right until a document uses `tfoot` early.
- `#captioned` measures the caption's own box, which spans the table and sits above the grid with a
  border-spacing gap below it.
- `#captionwide` measures the rule that a table is never narrower than its caption's longest word.
  A caption of one long word with a single narrow cell is the case where the caption alone decides
  the table's width. It is the caption's MIN-content width that applies, not its maximum: a long
  caption wraps rather than stretching the table out.
- `#aligned` measures `vertical-align` in a row taller than three of its cells. The default is
  `middle` rather than the `baseline` the property's initial value suggests, because the user-agent
  stylesheet sets it on the table and the cells inherit — so a converter that honours only the
  initial value puts every short cell's text at the top of its row.

The render is not quite pixel-identical. Two words differ on a glyph each, which is the sub-pixel
glyph positioning difference the todo records as the engine's largest residual. A table shows it
more readily than most layouts because column widths are fractional by nature, so almost no cell
starts on a whole pixel.

**Boxes**: 33 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0002 · SSIM 1.0000** |
| <img src="table/sections/reference_0001.png" width="480"> | <img src="table/sections/result%23page_0001.verified.png" width="480"> |


## table/spacing_borders

The separated border model: what `border-spacing` separates, and what a border and a background
land on once it has.

- `#spaced` sets both axes independently. The gap goes outside the first and last column as well as
  between them, which is what makes a table with spacing wider than the sum of its columns.
- `#framed` puts a border on the table and on two diagonally opposite cells, so the cell borders are
  measured against the table's own rather than confused with it. The cell borders are inside the
  spacing, not shared with a neighbour — that is what separated means.
- `#painted` fills a row in one table and a cell in another. A row's box spans the whole grid rather
  than only the cells in it, so a row background reaches across the spacing while a cell background
  stops at the cell.
- `#padded` gives one cell asymmetric padding, which has to widen its column and raise its row
  without disturbing the other cells.

`border-collapse: collapse` is not implemented and no scenario here asks for it. It is a different
model rather than a variation on this one — collapsed borders are shared between neighbours, and
half of each sits outside the cell it was declared on.

The render is not quite pixel-identical, and the cause is worth naming because it is not a layout
difference: the box geometry is exact. A column width is fractional, so a cell's border lands on a
fractional pixel, and the two engines resolve that differently — Chrome snaps a box decoration to
whole device pixels before painting it, while this draws the edge where the geometry puts it and
lets it antialias. The difference is a fraction of one pixel along each border edge, never more
than 40 levels of grey out of 255. It is the same effect `image/inline_flow` records for an image
edge.

**Boxes**: 31 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0003 · SSIM 0.9999** |
| <img src="table/spacing_borders/reference_0001.png" width="480"> | <img src="table/spacing_borders/result%23page_0001.verified.png" width="480"> |


## table/spans

Cells covering more than one slot, which is what makes a table a grid rather than rows of boxes.

- `#cols` has a spanning cell wider than the columns beneath it. The shortfall is shared in
  proportion to what those columns already wanted, so the wide cell does not put its extra width
  into a column with almost nothing in it. Sharing equally is the obvious alternative and is wrong.
- `#rows` covers two rows with one cell, which is the case that breaks naive column assignment: the
  second row's first cell belongs in the SECOND column, because the first is already taken. Getting
  that wrong shears every row below a span sideways, and it would still look plausible.
- `#stretch` forces the spanning cell taller than its rows need, and the extra is shared equally
  between them — the opposite of how a column shortfall is shared, and measured rather than assumed.
- `#mixed` combines both in one grid, so a row is entered with some columns already occupied and
  some cells spanning onward from it.

**Boxes**: 35 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="table/spans/reference_0001.png" width="480"> | <img src="table/spans/result%23page_0001.verified.png" width="480"> |


## text/kerning

Kerning pairs, at a size that makes a fraction of an em visible. AV, LT, Wa and To are the classic
ones: the pair is drawn tighter than the two advances would place it.

This is the scenario the corpus could not have before. Text used to be measured by summing raw hmtx
advances, which ignores kerning entirely, so `reset.css` disabled it in the browser to keep the two
sides comparable. Shaping through krilla's own rustybuzz removed that concession, and the third
paragraph checks the consequence that actually matters: with the wrong widths, a line breaks in the
wrong place.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0201 · SSIM 0.9945** |
| <img src="text/kerning/reference_0001.png" width="480"> | <img src="text/kerning/result%23page_0001.verified.png" width="480"> |


## text/ligatures

Ligatures, where shaping changes the glyph count rather than only the spacing: fi and fl become a
single glyph, so the text measures narrower than its characters would suggest.

The cluster mapping matters here as much as the width. A ligature covers several characters with
one glyph, so its text range spans them all — get that wrong and the PDF's text extraction returns
the wrong characters for the run even though the page looks right.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0099 · SSIM 0.9982** |
| <img src="text/ligatures/reference_0001.png" width="480"> | <img src="text/ligatures/result%23page_0001.verified.png" width="480"> |


## ua/acid1

Acid1, the CSS1 conformance test, from the W3C CSS1 test suite (`test5526c.htm`, dated 1998). The
only scenario here not written for this corpus, and it earns the exception: it exercises floats,
percentage widths, the box model and `clear` together, in an arrangement nobody designing a
scenario would have thought to build.

Two changes were made to the imported markup, both recorded so the import can be repeated:

- `Verdana` becomes `Liberation Sans`, since the corpus pins its fonts to the bundled faces. Text
  metrics change and no box does, which is what the test measures.
- The `<form>` and its two radio buttons are removed. Form widgets have no rendering here and the
  test itself exempts them — its own instructions say agents should match "except font
  rasterization and form widgets" — but a widget with no rendering is still a box the browser
  reports, so leaving them in would hold four boxes permanently unmatched and cost the corpus its
  property that geometry is zero everywhere.

It found two defects on its first render, neither of which any hand-written scenario had reached:

- The root element's background propagates to the CANVAS and covers the whole page rather than the
  root box alone. Acid1 paints the page blue and its content stops a third of the way down, so the error
  was two thirds of the page. `Inputs/reset.css` paints the root white over a page that was already
  white, which is why fifty-three scenarios had passed over it.
- `line-height` was not inherited, so `html { font: 10px/1 }` set the line height of the root and
  of nothing inside it. That is how nearly every stylesheet sets line spacing, and it left two
  boxes here a pixel too tall.

With both fixed it matches Chrome exactly.

**Boxes**: 14 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0009 · SSIM 1.0000** |
| <img src="ua/acid1/reference_0001.png" width="480"> | <img src="ua/acid1/result%23page_0001.verified.png" width="480"> |


## ua/blockquote_pre

Two elements whose defaults are more than a margin: blockquote is indented 40px on both sides, and
pre switches to the monospace family as well as preserving its white space. Both were absent from
AngleSharp's sheet.

**Boxes**: 6 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0005 · SSIM 0.9999** |
| <img src="ua/blockquote_pre/reference_0001.png" width="480"> | <img src="ua/blockquote_pre/result%23page_0001.verified.png" width="480"> |


## ua/headings

The six heading defaults, with no stylesheet of their own. Each has a distinct font size and a
distinct margin, and the margins run the opposite way to the sizes: the smallest heading has the
largest one. AngleSharp ships the HTML 4.01 values, which differ from the modern ones on every
level below h1, so this scenario is the whole reason UserAgentStyles.Corrections exists.

**Boxes**: 8 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0004 · SSIM 1.0000** |
| <img src="ua/headings/reference_0001.png" width="480"> | <img src="ua/headings/result%23page_0001.verified.png" width="480"> |


## ua/hr

# ua/hr

`<hr>` appeared nowhere in the corpus, despite having two pieces of machinery of its own:
`UserAgentStyles.Corrections` gives it `margin: 0.5em auto`, and `UnsupportedCss.BorderStyles`
exempts it by name from border-style reporting, because AngleSharp's default sheet makes every
plain `<hr>` `inset` and a page carrying no CSS at all would otherwise report four times.

Both were unmeasured. This scenario measures the first two directly — the half-em margins above
and below, and the automatic horizontal margins centring `#narrow` — and the third by having an
`<hr>` in the corpus at all, since `DiagnosticTests.TheCorpusReportsNothing` runs over every
scenario and would report a border style if the exemption were removed.

The rule itself is a zero-height box drawn entirely by its border, which is why it is visible at
all. This engine paints every border style solid, and the geometry of all seven boxes is exact, so
the 0.9911 is the shading of Chrome's `inset` against a solid line and nothing else. It is the
residual to expect here rather than a regression.

What to look at when it moves: `#narrow` against the left edge is `margin: auto` not resolving on
an `hr`. A rule touching the paragraphs above and below it is the half-em margins missing. A
change in the line's thickness or vertical position is the border, which is the whole box.

**Boxes**: 7 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0023 · SSIM 0.9911** |
| <img src="ua/hr/reference_0001.png" width="480"> | <img src="ua/hr/result%23page_0001.verified.png" width="480"> |


## ua/lists

List indentation, which AngleSharp omits entirely — before the corrections an unstyled list had no
padding-left at all and sat flush against the margin. Also covers the nested case, where the inner
list drops its vertical margins so a multi-level outline reads as one block.

List markers are not drawn, so the bullets and numbers a browser shows are absent from the render.
That is a real gap and it shows in the pixel metric; the box geometry is unaffected, because a
marker sits outside the principal box.

**Boxes**: 12 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0001 · SSIM 1.0000** |
| <img src="ua/lists/reference_0001.png" width="480"> | <img src="ua/lists/result%23page_0001.verified.png" width="480"> |


## ua/list_markers

The marker styles `ua/lists` does not reach: the third nesting level, the counter styles other than
decimal, and the three ways an ordered list can be numbered by something other than its position.

Each list isolates one rule:

- The unordered lists cycle disc, circle, square with depth, which is a user-agent rule rather than
  anything the author asked for.
- `start` and `value` both move the counter rather than only the item carrying them, so the item
  after `value="20"` is twenty-one.
- `reversed` counts down from the number of items, which is the one case that has to know how many
  items a list has before it can number any of them.
- The alphabetic list crossing 26 measures the one piece of non-obvious arithmetic in the counter
  styles. The alphabet is bijective base 26 — there is no zero digit — so 26 is `z` and 27 is `aa`,
  where ordinary base 26 would give `a` followed by nothing and skip a value at every power.
- `list-style-type: none` is here to check that it draws nothing rather than falling back to a
  marker, since an unrecognised value deliberately does fall back.
- The padded item measures what the marker is positioned against — the item's border edge, not its
  content edge — so its padding moves the text and leaves the bullet behind.
- The larger list measures the size rule, which steps in whole pixels off the item's own ascent.

Item text is deliberately short. A marker is a handful of pixels, so a scenario carrying sentences
would report mostly text rasterisation and a marker regression could hide inside the number.

Markers are invisible to the box comparison — they generate no element box, and the browser reports
no rect for them — so only the pixel metric measures anything here. The box geometry being exact
says the lists laid out correctly, not that anything was drawn.

The render is not pixel-identical, and the residual is two named things rather than a mystery:

- Every circular marker differs by a few levels of grey on its antialiased edge, because a circle
  reaching the PDF as four cubics is not bit-identical to the curve Chrome emits for the same
  circle. Largest on the 32px list, where the bullet is ten pixels across; nowhere above 14 of 255.
  The square marker and every counter marker are pixel-identical.
- The word "Twenty" differs on one glyph. That is the sub-pixel glyph positioning difference the
  todo records as the largest residual in the engine, and it has nothing to do with markers — it is
  the same effect `text/kerning` exists to measure.

**Boxes**: 32 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0002 · SSIM 1.0000** |
| <img src="ua/list_markers/reference_0001.png" width="480"> | <img src="ua/list_markers/result%23page_0001.verified.png" width="480"> |


## ua/paragraphs

Paragraph spacing with no stylesheet. The default margin is 1em top and bottom, and adjacent
paragraphs collapse to one gap rather than two. AngleSharp uses the HTML 4.01 value of 1.12em, so
before the corrections every gap in an unstyled document was 12% too large.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="ua/paragraphs/reference_0001.png" width="480"> | <img src="ua/paragraphs/result%23page_0001.verified.png" width="480"> |


