# Krilla.Html

Converts HTML to PDF. [AngleSharp](https://anglesharp.github.io/) parses the markup and runs the
CSS cascade, this library lays the result out, and [Krilla](https://www.nuget.org/packages/Krilla/)
writes the PDF.

```cs
using var fonts = new FontSet()
    .AddDirectory("fonts");

var pdf = HtmlConverter.Convert(
    "<h1>Hello</h1><p>World</p>",
    new()
    {
        Fonts = fonts
    });
```

Krilla has no font database, so the fonts a document may use are supplied by the caller rather
than discovered from the host. That is what makes output reproducible across machines.

## What is implemented

Block and inline layout, the box model, collapsing margins, line breaking, text alignment,
pagination, images, links, and text shaping with kerning and ligatures.

Images resolve from `data:` URIs and files relative to `BaseUrl`; nothing is fetched over the
network unless a caller supplies `HtmlOptions.ImageResolver`.

Floats, positioned boxes, flexbox, grid and tables lay out as plain blocks.
