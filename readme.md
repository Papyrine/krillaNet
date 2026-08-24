# <img src="/src/icon.png" height="30px"> Krilla

[![Build status](https://github.com/Papyrine/krillaNet/actions/workflows/build.yml/badge.svg)](https://github.com/Papyrine/krillaNet/actions/workflows/build.yml)
[![NuGet Status](https://img.shields.io/nuget/v/Krilla.svg?label=Krilla)](https://www.nuget.org/packages/Krilla/)

A .NET wrapper over [krilla](https://github.com/LaurenzV/krilla), the Rust PDF-writing library that backs [typst](https://typst.app). Creates PDF documents: pages, vector paths, gradients, text, and images, and converts HTML to PDF through the companion [Krilla.Html](https://www.nuget.org/packages/Krilla.Html/) package.

Krilla *writes* PDFs. To read, render or edit an existing one, use [Morph.PDFium](https://github.com/Papyrine/Morph.PDFium).

**See [Milestones](../../milestones?state=closed) for release notes.**


## NuGet packages

 * [Krilla](https://www.nuget.org/packages/Krilla/) — writes PDFs.
 * [Krilla.Html](https://www.nuget.org/packages/Krilla.Html/) — converts HTML to PDF.


## HTML to PDF

[Krilla.Html](https://www.nuget.org/packages/Krilla.Html/) converts HTML to PDF on top of this
library. [AngleSharp](https://anglesharp.github.io/) parses the markup and runs the CSS cascade,
`Krilla.Html` lays the result out, and Krilla writes the PDF.

<!-- snippet: HtmlToPdf -->
<a id='snippet-HtmlToPdf'></a>
```cs
using var fonts = new FontSet()
    .AddDirectory(fontDirectory);

var pdf = HtmlConverter.Convert(
    "<h1>Hello</h1><p>World</p>",
    new()
    {
        Fonts = fonts
    });
```
<sup><a href='/src/Krilla.Html.Tests/Samples.cs#L19-L31' title='Snippet source file'>snippet source</a> | <a href='#snippet-HtmlToPdf' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Krilla has no font database, so the fonts a document may use are supplied by the caller rather than
discovered from the host. That is what makes output reproducible across machines.

Implemented: block and inline layout, the box model including `box-sizing`, collapsing margins,
line breaking, text alignment, pagination, tables, floats, relative and absolute positioning,
images, and links —
`<a href>` becomes a real PDF link annotation, and a `#fragment` becomes an internal jump to
wherever that element paginated to. Flexbox and grid lay out as plain blocks.

Pagination honours forced breaks: `page-break-before` and `page-break-after` with a forced value
start a new page, and `page-break-inside: avoid` keeps a box whole by moving it to the next page
rather than splitting it. The modern `break-*` spellings are read as well. What is not honoured is
`avoid` at a box edge, `orphans` and `widows`, and the side a `left`/`right` value asks its page to
land on — all three are reported through `OnDiagnostic`.

Images resolve from `data:` URIs and from files relative to `BaseUrl`. Nothing is fetched over the
network by default — converting an untrusted document would otherwise issue requests to whatever
hosts it names. Set `HtmlOptions.ImageResolver` to take that decision explicitly, and the two
policies to bound what any resolver may load:

<!-- snippet: ImagePolicies -->
<a id='snippet-ImagePolicies'></a>
```cs
options.LocalImages = ImagePolicy.SafeDirectories(assetDirectory);
options.WebImages = ImagePolicy.SafeDomains("cdn.example.com");
```
<sup><a href='/src/Krilla.Html.Tests/Samples.cs#L50-L55' title='Snippet source file'>snippet source</a> | <a href='#snippet-ImagePolicies' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Both are checked before the resolver runs, so a refused source is never requested. A `data:` URI
is never gated, since its bytes are already in the document.

Because the engine implements a subset of CSS and lays the rest out as a plain block, a document
using an unimplemented construct comes out wrong with nothing to say so. `OnDiagnostic` turns that
into a report:

<!-- snippet: Diagnostics -->
<a id='snippet-Diagnostics'></a>
```cs
options.OnDiagnostic = diagnostic => Console.WriteLine(diagnostic);

// <div> display: flex — laid out as a block
// <table> border-collapse: collapse — laid out with the separated border model
// <p> align: left — not applied, because presentational attributes are not mapped onto the cascade
// <img> src: logo.png — did not resolve to an image, so no box was generated
```
<sup><a href='/src/Krilla.Html.Tests/Samples.cs#L75-L84' title='Snippet source file'>snippet source</a> | <a href='#snippet-Diagnostics' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Unrecognised CSS is deliberately not reported. Listing every `cursor` and `content` an ordinary
stylesheet carries would bury the signal, and would cost the invariant that makes the sink worth
subscribing to: a conversion that reports nothing laid out every construct the way a browser would.

Two limits worth knowing before reaching for it:

- Text is shaped through krilla's own shaper, so kerning and ligatures are applied. Bidirectional
  resolution, font fallback and complex-script shaping are still missing, so a run is shaped with
  one font and one script.
- Lines break at spaces, at hyphens and dashes, and either side of an image or inline-block. There
  is no hyphenation dictionary, no soft-hyphen support, and no Unicode line breaking algorithm
  beyond that — so scripts that wrap without spaces overflow rather than wrapping.
- AngleSharp compares CSS specificity across cascade origins, where the specification resolves
  origin first. A reset relying on `* { margin: 0 }` will not clear the default margins on `body`
  and `p`; name the elements explicitly instead.

The default stylesheet AngleSharp ships is the HTML 4.01 one, which disagrees with browsers on most
block elements — headings, paragraph spacing, and list indentation in particular. `Krilla.Html`
appends corrections from the HTML Standard's rendering section, so an unstyled document matches a
browser.

Layout fidelity is measured against Chrome scenario by scenario — see
[the corpus](/src/Krilla.Html.Tests/Inputs/readme.md).


### A specimen page

[`showcase.html`](/src/Krilla.Html.Tests/Showcase/showcase.html) is one hand-written document that
combines most of the above at once: a positioned stamp on the masthead, a floated side panel that
shortens the lines beside it, justified and shaped text, a bar chart drawn entirely out of boxes
and percentage widths, a table, and a real link annotation in the footer. The picture is page one
of the PDF, rendered through PDFium at 96 dpi. Click it for the PDF itself.

| [<img src="/src/Krilla.Html.Tests/Showcase/ShowcaseTests.Specimen%23page_0001.verified.png" width="320" alt="A page of the specimen document converted to PDF">](/src/Krilla.Html.Tests/Showcase/ShowcaseTests.Specimen.verified.pdf) |
| --- |

Both artefacts are produced by [`ShowcaseTests`](/src/Krilla.Html.Tests/Showcase/ShowcaseTests.cs),
which also asserts the conversion reports no diagnostics — so nothing on that page was laid out as
something other than what it asks for.


## Usage

Every snippet below is a test, and the image under it is that test's snapshot: the first page
of the PDF the code actually produces, rendered through [PDFium](https://github.com/Papyrine/Morph.PDFium).
Click one for the full PDF. Both are regenerated whenever the sample changes, so the code and
the picture cannot drift apart.


### Hello world

<!-- snippet: HelloWorld -->
<a id='snippet-HelloWorld'></a>
```cs
using var document = new KrillaDocument();

using (var page = document.StartPage(PageSettings.A4))
{
    page.Surface.FillRectangle(
        Rectangle.FromSize(50, 50, 200, 100),
        Color.Rgb(220, 40, 40));
}

var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L22-L35' title='Snippet source file'>snippet source</a> | <a href='#snippet-HelloWorld' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


#### Result

| [<img src="/src/Krilla.Tests/Samples.HelloWorld%23page_0001.verified.png" width="190" alt="Rendered output of the HelloWorld sample">](/src/Krilla.Tests/Samples.HelloWorld.verified.pdf) |
| --- |


### Draw a path

<!-- snippet: DrawAPath -->
<a id='snippet-DrawAPath'></a>
```cs
using var document = new KrillaDocument();
using var paint = Paint.Solid(Color.Rgb(30, 90, 200));

using (var page = document.StartPage(300, 200))
{
    using var path = new PathBuilder()
        .MoveTo(20, 20)
        .LineTo(280, 20)
        .LineTo(150, 180)
        .Close()
        .Build();

    page.Surface
        .SetFill(paint)
        .DrawPath(path);
}

var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L43-L64' title='Snippet source file'>snippet source</a> | <a href='#snippet-DrawAPath' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


#### Result

| [<img src="/src/Krilla.Tests/Samples.DrawAPath%23page_0001.verified.png" width="260" alt="Rendered output of the DrawAPath sample">](/src/Krilla.Tests/Samples.DrawAPath.verified.pdf) |
| --- |


### Fill and stroke

Fill and stroke are independent pieces of surface state. Setting both draws both; setting neither makes krilla fall back to filling black rather than drawing nothing.

<!-- snippet: FillAndStroke -->
<a id='snippet-FillAndStroke'></a>
```cs
using var document = new KrillaDocument();
using var fill = Paint.Solid(Color.Rgb(250, 220, 120));
using var outline = Paint.Solid(Color.Rgb(60, 60, 60));

using (var page = document.StartPage(200, 200))
{
    using var path = PdfPath.Rectangle(Rectangle.FromSize(40, 40, 120, 120));

    // Fill and stroke are independent state; setting both draws both.
    page.Surface
        .SetFill(fill)
        .SetStroke(new Stroke(outline, Width: 4, DashArray: [8, 4]))
        .DrawPath(path);
}

var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L72-L91' title='Snippet source file'>snippet source</a> | <a href='#snippet-FillAndStroke' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


#### Result

| [<img src="/src/Krilla.Tests/Samples.FillAndStroke%23page_0001.verified.png" width="190" alt="Rendered output of the FillAndStroke sample">](/src/Krilla.Tests/Samples.FillAndStroke.verified.pdf) |
| --- |


### Gradients

<!-- snippet: GradientFill -->
<a id='snippet-GradientFill'></a>
```cs
using var document = new KrillaDocument();
using var gradient = Paint.LinearGradient(
    0, 0, 300, 0,
    [
        new(0f, Color.Rgb(255, 90, 0)),
        new(1f, Color.Rgb(0, 90, 255))
    ]);

using (var page = document.StartPage(300, 150))
{
    using var path = PdfPath.Rectangle(new(0, 0, 300, 150));
    page.Surface.SetFill(gradient).DrawPath(path);
}

var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L99-L117' title='Snippet source file'>snippet source</a> | <a href='#snippet-GradientFill' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

All stops in a gradient must share one colour space. A mismatch is reported when the document is finished, not when the gradient is created.


#### Result

| [<img src="/src/Krilla.Tests/Samples.GradientFill%23page_0001.verified.png" width="260" alt="Rendered output of the GradientFill sample">](/src/Krilla.Tests/Samples.GradientFill.verified.pdf) |
| --- |


### Transforms, clips and opacity

These use a push/pop stack, which krilla requires to be balanced. Each push returns a `Layer` whose disposal pops it, so a `using` statement makes the pairing structural.

<!-- snippet: TransformsAndOpacity -->
<a id='snippet-TransformsAndOpacity'></a>
```cs
using var document = new KrillaDocument();

using (var page = document.StartPage(200, 200))
{
    var surface = page.Surface;

    // Each push is reverted when its layer is disposed, so the pairing krilla
    // requires is structural rather than something to remember.
    using (surface.PushTransform(Matrix.Translate(100, 100)))
    using (surface.PushOpacity(0.5f))
    {
        surface.FillRectangle(new(-50, -50, 50, 50), Color.Rgb(0, 160, 90));
    }
}

var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L125-L144' title='Snippet source file'>snippet source</a> | <a href='#snippet-TransformsAndOpacity' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


#### Result

| [<img src="/src/Krilla.Tests/Samples.TransformsAndOpacity%23page_0001.verified.png" width="190" alt="Rendered output of the TransformsAndOpacity sample">](/src/Krilla.Tests/Samples.TransformsAndOpacity.verified.pdf) |
| --- |


### Multiple pages

<!-- snippet: MultiplePages -->
<a id='snippet-MultiplePages'></a>
```cs
using var document = new KrillaDocument();

foreach (var index in Enumerable.Range(0, 3))
{
    using var page = document.StartPage(PageSettings.Letter);
    page.Surface.FillRectangle(
        Rectangle.FromSize(72, 72 * (index + 1), 200, 40),
        Color.Gray(80));
}

document.Save("report.pdf");
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L152-L166' title='Snippet source file'>snippet source</a> | <a href='#snippet-MultiplePages' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


#### Result

| [<img src="/src/Krilla.Tests/Samples.MultiplePages%23page_0001.verified.png" width="190" alt="Rendered output of the MultiplePages sample">](/src/Krilla.Tests/Samples.MultiplePages.verified.pdf) |
| --- |


### Images

<!-- snippet: DrawAnImage -->
<a id='snippet-DrawAnImage'></a>
```cs
using var document = new KrillaDocument();

// Raw RGBA, four bytes per pixel, row-major.
var pixels = new byte[4 * 4 * 4];

for (var y = 0; y < 4; y++)
{
    for (var x = 0; x < 4; x++)
    {
        var offset = (y * 4 + x) * 4;
        var dark = (x + y) % 2 == 0;

        pixels[offset] = dark ? (byte) 40 : (byte) 230;
        pixels[offset + 1] = dark ? (byte) 110 : (byte) 230;
        pixels[offset + 2] = dark ? (byte) 190 : (byte) 230;
        pixels[offset + 3] = 255;
    }
}

using var image = PdfImage.FromRgba(pixels, 4, 4);

using (var page = document.StartPage(120, 120))
{
    page.Surface.DrawImage(image, Rectangle.FromSize(10, 10, 100, 100));
}

var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L174-L204' title='Snippet source file'>snippet source</a> | <a href='#snippet-DrawAnImage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


#### Result

| [<img src="/src/Krilla.Tests/Samples.DrawAnImage%23page_0001.verified.png" width="120" alt="Rendered output of the DrawAnImage sample">](/src/Krilla.Tests/Samples.DrawAnImage.verified.pdf) |
| --- |


### Metadata

<!-- snippet: Metadata -->
<a id='snippet-Metadata'></a>
```cs
using var document = new KrillaDocument();

document.SetMetadata(
    new()
    {
        Title = "Quarterly Report",
        Language = "en-GB",
        Authors = ["A. Writer"],
        Keywords = ["quarterly", "report"],
        CreationDate = created
    });

using (var page = document.StartPage(PageSettings.A4))
{
    page.Surface.FillRectangle(new(72, 72, 523, 130), Color.Gray(40));
}

var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L212-L233' title='Snippet source file'>snippet source</a> | <a href='#snippet-Metadata' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


#### Result

| [<img src="/src/Krilla.Tests/Samples.Metadata%23page_0001.verified.png" width="190" alt="Rendered output of the Metadata sample">](/src/Krilla.Tests/Samples.Metadata.verified.pdf) |
| --- |


### Bookmarks

<!-- snippet: Bookmarks -->
<a id='snippet-Bookmarks'></a>
```cs
using var document = new KrillaDocument();

foreach (var index in Enumerable.Range(0, 3))
{
    using var page = document.StartPage(PageSettings.A4);
    page.Surface.FillRectangle(
        Rectangle.FromSize(72, 72, 200 + index * 60, 32),
        Color.Gray((byte) (40 + index * 60)));
}

var chapter = new OutlineItem("Chapter One", pageIndex: 0)
{
    IsOpen = true
};
chapter.Add("Section 1.1", pageIndex: 1);

document.SetOutline(chapter, new OutlineItem("Chapter Two", pageIndex: 2));

var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L241-L263' title='Snippet source file'>snippet source</a> | <a href='#snippet-Bookmarks' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


#### Result

| [<img src="/src/Krilla.Tests/Samples.Bookmarks%23page_0001.verified.png" width="190" alt="Rendered output of the Bookmarks sample">](/src/Krilla.Tests/Samples.Bookmarks.verified.pdf) |
| --- |


### Links

<!-- snippet: Links -->
<a id='snippet-Links'></a>
```cs
using var document = new KrillaDocument();

using (var page = document.StartPage(PageSettings.A4))
{
    var surface = page.Surface;

    // Nothing about a link is visible on its own, so draw the text it sits over.
    surface.FillRectangle(new(72, 72, 300, 100), Color.Rgb(20, 80, 200));
    surface.AddLink(new(72, 72, 300, 100), "https://example.com/");

    surface.FillRectangle(new(72, 120, 300, 148), Color.Rgb(20, 80, 200));
    // Internal — the target page need not exist yet.
    surface.AddLink(new(72, 120, 300, 148), pageIndex: 1);
}

using (document.StartPage(PageSettings.A4))
{
}

var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L271-L294' title='Snippet source file'>snippet source</a> | <a href='#snippet-Links' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


#### Result

| [<img src="/src/Krilla.Tests/Samples.Links%23page_0001.verified.png" width="190" alt="Rendered output of the Links sample">](/src/Krilla.Tests/Samples.Links.verified.pdf) |
| --- |


### Attachments

<!-- snippet: Attachments -->
<a id='snippet-Attachments'></a>
```cs
using var document = new KrillaDocument();

using (var page = document.StartPage(PageSettings.A4))
{
    page.Surface.FillRectangle(new(72, 72, 523, 160), Color.Gray(200));
}

document.EmbedFile(
    "source-data.csv",
    "name,value\nalpha,1\n"u8,
    mimeType: "text/csv",
    description: "The data behind the chart",
    association: FileAssociation.Data);

var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L420-L438' title='Snippet source file'>snippet source</a> | <a href='#snippet-Attachments' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


#### Result

| [<img src="/src/Krilla.Tests/Samples.Attachments%23page_0001.verified.png" width="190" alt="Rendered output of the Attachments sample">](/src/Krilla.Tests/Samples.Attachments.verified.pdf) |
| --- |


### Reusable graphics

Content captured once and drawn repeatedly costs almost nothing in file size: krilla emits the stream once and references it. The same mechanism backs soft masks (`PushMask`) and tiling patterns (`CapturePattern`).

<!-- snippet: ReusableGraphic -->
<a id='snippet-ReusableGraphic'></a>
```cs
using var document = new KrillaDocument();

using (var page = document.StartPage(PageSettings.A4))
{
    // Captured once, emitted once, referenced many times.
    using var stamp = page.Surface.CaptureGraphic(
        surface => surface.FillRectangle(new(0, 0, 40, 40), Color.Rgb(200, 30, 30)));

    foreach (var index in Enumerable.Range(0, 20))
    {
        using (page.Surface.PushTransform(Matrix.Translate(72 + index * 20, 72)))
        {
            page.Surface.DrawGraphic(stamp);
        }
    }
}

var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L391-L412' title='Snippet source file'>snippet source</a> | <a href='#snippet-ReusableGraphic' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


#### Result

| [<img src="/src/Krilla.Tests/Samples.ReusableGraphic%23page_0001.verified.png" width="190" alt="Rendered output of the ReusableGraphic sample">](/src/Krilla.Tests/Samples.ReusableGraphic.verified.pdf) |
| --- |


### PDF/A

<!-- snippet: ArchivalPdf -->
<a id='snippet-ArchivalPdf'></a>
```cs
using var document = new KrillaDocument(
    new()
    {
        Archival = PdfArchival.A2B,
        XmpMetadata = true
    });

document.SetMetadata(
    new()
    {
        Title = "Archived Invoice",
        Language = "en-GB",
        CreationDate = created
    });

using (var page = document.StartPage(PageSettings.A4))
{
    page.Surface.FillRectangle(new(72, 72, 523, 200), Color.Gray(220));
}

// Conformance violations are reported here, as a KrillaException, rather than when
// the offending content was added.
var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L302-L328' title='Snippet source file'>snippet source</a> | <a href='#snippet-ArchivalPdf' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


#### Result

| [<img src="/src/Krilla.Tests/Samples.ArchivalPdf%23page_0001.verified.png" width="190" alt="Rendered output of the ArchivalPdf sample">](/src/Krilla.Tests/Samples.ArchivalPdf.verified.pdf) |
| --- |


### Tagged PDF and PDF/UA

An accessible PDF needs a logical structure tree: a hierarchy of headings, paragraphs, lists and tables that a screen reader navigates, separate from the drawing order. Mark spans of content as they are drawn, then assemble the identifiers into a tree.

<!-- snippet: AccessibleDocument -->
<a id='snippet-AccessibleDocument'></a>
```cs
using var document = new KrillaDocument(
    new()
    {
        EnableTagging = true,
        Accessibility = PdfAccessibility.Ua1
    });

// PDF/UA requires a title, a language and an outline. Omitting any of them fails at
// Finish, with the specific rule named in the exception.
document.SetMetadata(
    new()
    {
        Title = "An Accessible Document",
        Language = "en-GB"
    });

TagIdentifier headingContent;
TagIdentifier bodyContent;

using (var page = document.StartPage(PageSettings.A4))
{
    var surface = page.Surface;

    // Each tagged span yields an identifier that goes into the structure tree.
    headingContent = surface.BeginText();
    surface.FillRectangle(new(72, 72, 523, 100), Color.Gray(30));
    surface.EndTagged();

    bodyContent = surface.BeginText();
    surface.FillRectangle(new(72, 120, 523, 400), Color.Gray(160));
    surface.EndTagged();
}

using var tree = new TagTree();
tree.WithLanguage("en-GB");

var section = tree.Add(TagKind.Section);
section.Add(Tag.Heading(1, "Introduction")).Add(headingContent);
section.Add(TagKind.Paragraph).Add(bodyContent);

document.SetTagTree(tree);
document.SetOutline(new OutlineItem("Introduction", pageIndex: 0));

var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L336-L383' title='Snippet source file'>snippet source</a> | <a href='#snippet-AccessibleDocument' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Content that is decoration rather than meaning — running heads, page numbers, rules — should be marked with `BeginArtifact` instead, which keeps it out of the tree entirely.


#### Result

| [<img src="/src/Krilla.Tests/Samples.AccessibleDocument%23page_0001.verified.png" width="190" alt="Rendered output of the AccessibleDocument sample">](/src/Krilla.Tests/Samples.AccessibleDocument.verified.pdf) |
| --- |


## Behavior notes

 * The surface origin is the **top-left** corner, with Y increasing downward. PDF's own coordinate system is Y-up; krilla applies the flip.
 * Sizes are in points — 72 to the inch. `PageSettings.A4` is 595 x 842 and `PageSettings.Letter` is 612 x 792.
 * **One page can be open at a time.** krilla keeps global serialization state while a page is open, so starting a second page before closing the first throws. `Page` is `IDisposable`, so a `using` statement handles it.
 * `Finish()` consumes the document. Nothing can be added afterwards.
 * A document with no pages still produces a valid PDF: krilla writes a single empty page, because a page-less PDF is invalid.
 * **There is no font database.** krilla does not enumerate installed fonts and does not match on family or style. `Font.Load` takes bytes, and finding those bytes is the caller's job.
 * Documents are **not thread safe** and carry no internal locking — unlike Morph.PDFium, which serializes on a process-wide lock because PDFium has global state. Krilla has none, so separate documents are genuinely independent and can be built in parallel; a single document must be used from one thread at a time.
 * Output is deterministic for a given Krilla version, which makes it suitable for snapshot testing.


## Native binaries

The package carries `runtimes/<rid>/native/` entries for eight RIDs: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64` and `osx-arm64`. NuGet copies only the one matching the target runtime, so `dotnet publish -r linux-x64` emits a single `libkrilla_capi.so`.

A runtime-agnostic `dotnet build` copies the whole tree into `bin`. To avoid that, set a runtime identifier:

```xml
<PropertyGroup>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
</PropertyGroup>
```

The Linux builds are linked against glibc 2.17, so they load on every currently supported distribution. Separate musl builds are shipped because .NET's RID graph does not fall back from `linux-musl-x64` to `linux-x64`; without them an Alpine container would fail with a bare `DllNotFoundException`.

Building from source needs [Rust](https://rustup.rs) — `dotnet build` shells out to cargo when the native is missing or stale, and skips silently when cargo is absent. Consumers of the package never need Rust.


## Third-party licences

Krilla itself is [MIT](license.txt). The native library statically links krilla and its dependency tree, so their licences are reproduced in [THIRD-PARTY-NOTICES.md](src/Krilla/THIRD-PARTY-NOTICES.md), which ships inside the package.


## Icon

Icon attribution pending — the placeholder is currently shared with [Morph.PDFium](https://github.com/Papyrine/Morph.PDFium) and must be replaced before the first public release.
