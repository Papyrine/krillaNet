# All scenarios (41)

The browser reference (left) beside the page Krilla.Html produced (right). `AE` is the fraction of pixels that differ and `SSIM` is structural similarity; neither is asserted. The worst offset is the largest positional disagreement in CSS pixels between the rendered element geometry and the browser's, and is the number to watch — it reaches zero exactly when the layout is right.

<details>
<summary>Contents</summary>

- [block/auto_margins](#block-auto_margins)
- [block/auto_width](#block-auto_width)
- [block/background_color](#block-background_color)
- [block/borders](#block-borders)
- [block/box_model](#block-box_model)
- [block/margin_collapse](#block-margin_collapse)
- [block/margin_collapse_blocked](#block-margin_collapse_blocked)
- [block/margin_collapse_parent](#block-margin_collapse_parent)
- [block/max_width](#block-max_width)
- [block/nested_blocks](#block-nested_blocks)
- [block/percentage_width](#block-percentage_width)
- [image/block_centred](#image-block_centred)
- [image/data_uri](#image-data_uri)
- [image/inline_flow](#image-inline_flow)
- [image/intrinsic](#image-intrinsic)
- [image/max_width](#image-max_width)
- [image/sized](#image-sized)
- [inline/font_size_em](#inline-font_size_em)
- [inline/font_style](#inline-font_style)
- [inline/font_weight](#inline-font_weight)
- [inline/justify](#inline-justify)
- [inline/line_height](#inline-line_height)
- [inline/line_height_normal](#inline-line_height_normal)
- [inline/nested_inline](#inline-nested_inline)
- [inline/simple_text](#inline-simple_text)
- [inline/text_align](#inline-text_align)
- [inline/white_space_pre](#inline-white_space_pre)
- [inline/wrapping](#inline-wrapping)
- [link/external](#link-external)
- [link/fragment](#link-fragment)
- [link/wrapped](#link-wrapped)
- [page/break_between_lines](#page-break_between_lines)
- [page/multi_page_flow](#page-multi_page_flow)
- [page/page_size](#page-page_size)
- [text/kerning](#text-kerning)
- [text/ligatures](#text-ligatures)
- [ua/blockquote_pre](#ua-blockquote_pre)
- [ua/headings](#ua-headings)
- [ua/lists](#ua-lists)
- [ua/list_markers](#ua-list_markers)
- [ua/paragraphs](#ua-paragraphs)

</details>

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


## block/nested_blocks

Containing widths through three levels. Each auto width is its parent content width, so an error in
the border or padding subtraction compounds visibly rather than staying hidden at one level.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="block/nested_blocks/reference_0001.png" width="480"> | <img src="block/nested_blocks/result%23page_0001.verified.png" width="480"> |


## block/percentage_width

Percentages resolve against the containing block CONTENT width, which is 600 here and not 640.
Percentage margins resolve against that same width, not against the height.

**Boxes**: 5 matched, worst offset 0.00px, worst size 0.00px.

| Reference (Chrome) | Krilla.Html |
| --- | --- |
| **Page 1** | **Page 1. AE 0.0000 · SSIM 1.0000** |
| <img src="block/percentage_width/reference_0001.png" width="480"> | <img src="block/percentage_width/result%23page_0001.verified.png" width="480"> |


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


