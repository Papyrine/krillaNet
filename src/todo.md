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

41 scenarios across 7 categories. **Box geometry matches Chrome exactly on every one** — zero
positional and zero size difference — and 33 read SSIM 1.0000.

The eight that do not, with the cause of each. None is a mystery, and none should be "fixed" by
regenerating a baseline:

| Scenario | AE | SSIM | Cause |
| --- | --- | --- | --- |
| `text/kerning` | 0.0201 | 0.9945 | Sub-pixel glyph positioning, below |
| `text/ligatures` | 0.0099 | 0.9982 | Same |
| `page/break_between_lines` | 0.0017 | 0.9996 | Same |
| `image/inline_flow` | 0.0007 | 0.9998 | Image edge at a fractional x |
| `page/multi_page_flow` | 0.0016 | 0.9998 | Sub-pixel glyph positioning |
| `link/fragment`, `link/wrapped` | 0.0001 | 0.9999 | Same |
| `ua/blockquote_pre` | 0.0005 | 0.9999 | Same |

SSIM 1.0000 is not the same as pixel-identical, and six scenarios sit in the gap: `block/borders`,
`ua/lists`, `ua/list_markers`, `inline/justify`, `link/external` and `ua/headings` each differ on a
scattering of antialiased pixels. For the first three the cause is named — a circle reaching the PDF
as four cubics is not bit-identical to the curve Chrome emits, and a mitre diagonal is not
bit-identical either — and none exceeds 14 levels of grey out of 255.

## Unimplemented layout

Each of these currently lays out as a plain block. That is deliberate — a wrong box keeps the
content on the page and shows up as a geometry difference, where dropping the element would leave
nothing for the corpus to measure — but it means a real document using any of them is wrong in a
way the corpus does not yet report, because no scenario covers them.

- **Tables.** Probably the most valuable next piece: invoices, statements and reports are the
  obvious use for HTML to PDF, and every one of them is a table. Needs the fixed and automatic
  table layout algorithms, row and column spanning, and border collapsing. Large.
- **Floats and `clear`.** Substantially harder than it looks, because a float shortens the line
  boxes beside it, so float placement and line layout stop being separable.
- **`position: relative` / `absolute` / `fixed`.** Absolute positioning needs a containing-block
  chain that tracks the nearest positioned ancestor; `fixed` in paged media needs a decision about
  what "the viewport" means on paper.
- **Flexbox**, then **grid**. Modern documents use them, but neither is worth starting before the
  block substrate has tables and floats.

## Text

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
- **`small` and `big`** are not honoured: the `smaller`/`larger` keywords fall through to the
  inherited size. The HTML default stylesheet uses them, so `<small>` currently renders at full
  size.
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
  change to inline layout rather than to painting. Nothing measures it. Drawing the marker at the
  content edge without shortening the line would overlap the text, which is why it renders as
  `outside` rather than approximately right.
- **`list-style-image` and the `type` content attribute are ignored.** `<ol type="a">` numbers in
  decimal, because HTML's presentational-hint mapping for it is not applied — the same gap
  `<img width>` used to have, and fixable the same way.
- **An empty `<li>` is zero high**, where a browser gives it a line box for its marker to sit on.
  The marker is still drawn, at the baseline that line would have had, so it lands on top of
  whatever follows. Nothing measures it.
- **No `overflow` handling.** Content larger than a box with an explicit height paints outside it
  and is never clipped, because only the page box clips.
- **No `box-sizing: border-box`.** Only the initial `content-box` is honoured, and border-box is
  what most real stylesheets set.
- **Percentage heights resolve as `auto`.** Correct whenever the containing height is indefinite,
  which it usually is in paged media, but wrong for a box inside one with a definite height.
- **No background images or gradients**, no `opacity`, no `transform`, no `border-radius`.

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
