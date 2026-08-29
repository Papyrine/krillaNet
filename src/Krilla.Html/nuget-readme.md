# Krilla.Html

Converts HTML to PDF. [AngleSharp](https://anglesharp.github.io/) parses the markup and runs the
CSS cascade, this library lays the result out, and [Krilla](https://www.nuget.org/packages/Krilla/)
writes the PDF.

```cs
using var fonts = new FontSet()
    .AddDirectory("fonts");

var pdf = await HtmlConverter.ConvertAsync(
    "<h1>Hello</h1><p>World</p>",
    new()
    {
        Fonts = fonts
    });
```

Krilla has no font database, so the fonts a document may use are supplied by the caller rather
than discovered from the host. That is what makes output reproducible across machines.

## What is implemented

Block and inline layout, inline-block, tables, floats, relative and absolute positioning, the box
model including `box-sizing`, collapsing margins, line breaking, text alignment, pagination,
images — raster and SVG alike — links, list markers, and text shaping with kerning and ligatures.

Also `calc()` and the viewport units, the inline box model, raster background images with the five
properties that place them, soft hyphens, `overflow-wrap` and `word-break`, tabs under `pre`, text
decorations with their colour and style, `opacity`, 2D transforms, gradients, `border-radius`, both
table border models, forced page breaks, `@page` rules, print media queries, generated content with
CSS counters, `aspect-ratio`, `rgba()` colours, shadows as offsets including `inset`, `<col>` widths, every value
`border-style` takes including the four bevelled ones, and the `ex` and `ch` units.

`@page`'s sixteen margin boxes carry running headers, footers and page numbers, with
`counter(page)`, `counter(pages)` and the `:first`/`:left`/`:right`/`:blank` page selectors. A
table's `<thead>` is re-drawn at the top of every page its table continues onto, and a
`position: fixed` box is drawn on every page. `orphans` and `widows` constrain where a page breaks.

The PDF gets a bookmark tree from the document's headings, a named destination for every `id`, and
its title and language from the document when the caller has not set them.

Images resolve from `data:` URIs and files relative to `BaseUrl` — including a `url()` in a
stylesheet, which goes through the same resolver and the same policies. Nothing is fetched over the
network unless a caller supplies `HtmlOptions.ImageResolver`.

Flexbox is implemented; grid lays out as a plain block. `HtmlOptions.OnDiagnostic` reports every construct that
is recognised and not rendered the way a browser would, so a conversion that reports nothing laid
out the whole document correctly.
