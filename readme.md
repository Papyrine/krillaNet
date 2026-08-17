# <img src="/src/icon.png" height="30px"> Krilla

[![Build status](https://github.com/Papyrine/krillaNet/actions/workflows/build.yml/badge.svg)](https://github.com/Papyrine/krillaNet/actions/workflows/build.yml)
[![NuGet Status](https://img.shields.io/nuget/v/Krilla.svg?label=Krilla)](https://www.nuget.org/packages/Krilla/)

A .NET wrapper over [krilla](https://github.com/LaurenzV/krilla), the Rust PDF-writing library that backs [typst](https://typst.app). Creates PDF documents: pages, vector paths, gradients, text, and images. No external dependency — the native library ships inside the package.

Krilla *writes* PDFs. To read, render or edit an existing one, use [Morph.PDFium](https://github.com/Papyrine/Morph.PDFium).

**See [Milestones](../../milestones?state=closed) for release notes.**


## NuGet package

[Krilla](https://www.nuget.org/packages/Krilla/)


## Usage


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
<sup><a href='/src/Krilla.Tests/Samples.cs#L9-L22' title='Snippet source file'>snippet source</a> | <a href='#snippet-HelloWorld' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


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
<sup><a href='/src/Krilla.Tests/Samples.cs#L30-L51' title='Snippet source file'>snippet source</a> | <a href='#snippet-DrawAPath' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


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
<sup><a href='/src/Krilla.Tests/Samples.cs#L59-L78' title='Snippet source file'>snippet source</a> | <a href='#snippet-FillAndStroke' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


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
<sup><a href='/src/Krilla.Tests/Samples.cs#L86-L104' title='Snippet source file'>snippet source</a> | <a href='#snippet-GradientFill' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

All stops in a gradient must share one colour space. A mismatch is reported when the document is finished, not when the gradient is created.


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
<sup><a href='/src/Krilla.Tests/Samples.cs#L112-L131' title='Snippet source file'>snippet source</a> | <a href='#snippet-TransformsAndOpacity' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


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

document.Save(Path.Combine(Path.GetTempPath(), "krilla-sample.pdf"));
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L139-L153' title='Snippet source file'>snippet source</a> | <a href='#snippet-MultiplePages' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Images

<!-- snippet: DrawAnImage -->
<a id='snippet-DrawAnImage'></a>
```cs
using var document = new KrillaDocument();

// Raw RGBA, four bytes per pixel, row-major.
var pixels = new byte[4 * 4 * 4];
Array.Fill(pixels, (byte) 200);

using var image = PdfImage.FromRgba(pixels, 4, 4);

using (var page = document.StartPage(120, 120))
{
    page.Surface.DrawImage(image, Rectangle.FromSize(10, 10, 100, 100));
}

var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L161-L178' title='Snippet source file'>snippet source</a> | <a href='#snippet-DrawAnImage' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


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
        CreationDate = DateTimeOffset.UtcNow
    });

using (document.StartPage(PageSettings.A4))
{
}

var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L186-L205' title='Snippet source file'>snippet source</a> | <a href='#snippet-Metadata' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Bookmarks

<!-- snippet: Bookmarks -->
<a id='snippet-Bookmarks'></a>
```cs
using var document = new KrillaDocument();

foreach (var _ in Enumerable.Range(0, 3))
{
    using (document.StartPage(PageSettings.A4))
    {
    }
}

var chapter = new OutlineItem("Chapter One", pageIndex: 0)
{
    IsOpen = true
};
chapter.Add("Section 1.1", pageIndex: 1);

document.SetOutline(chapter, new OutlineItem("Chapter Two", pageIndex: 2));

var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L213-L234' title='Snippet source file'>snippet source</a> | <a href='#snippet-Bookmarks' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Links

<!-- snippet: Links -->
<a id='snippet-Links'></a>
```cs
using var document = new KrillaDocument();

using (var page = document.StartPage(PageSettings.A4))
{
    // External.
    page.Surface.AddLink(new(72, 72, 300, 100), "https://example.com/");

    // Internal — the target page need not exist yet.
    page.Surface.AddLink(new(72, 120, 300, 148), pageIndex: 1);
}

using (document.StartPage(PageSettings.A4))
{
}

var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L242-L261' title='Snippet source file'>snippet source</a> | <a href='#snippet-Links' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


### Attachments

<!-- snippet: Attachments -->
<a id='snippet-Attachments'></a>
```cs
using var document = new KrillaDocument();

using (document.StartPage(PageSettings.A4))
{
}

document.EmbedFile(
    "source-data.csv",
    "name,value\nalpha,1\n"u8,
    mimeType: "text/csv",
    description: "The data behind the chart",
    association: FileAssociation.Data);

var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L387-L404' title='Snippet source file'>snippet source</a> | <a href='#snippet-Attachments' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


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
<sup><a href='/src/Krilla.Tests/Samples.cs#L358-L379' title='Snippet source file'>snippet source</a> | <a href='#snippet-ReusableGraphic' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


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
        CreationDate = DateTimeOffset.UtcNow
    });

using (var page = document.StartPage(PageSettings.A4))
{
    page.Surface.FillRectangle(new(72, 72, 523, 200), Color.Rgb(240, 240, 240));
}

// Conformance violations are reported here, as a KrillaException, rather than when
// the offending content was added.
var pdf = document.Finish();
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L269-L295' title='Snippet source file'>snippet source</a> | <a href='#snippet-ArchivalPdf' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->


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
<sup><a href='/src/Krilla.Tests/Samples.cs#L303-L350' title='Snippet source file'>snippet source</a> | <a href='#snippet-AccessibleDocument' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Content that is decoration rather than meaning — running heads, page numbers, rules — should be marked with `BeginArtifact` instead, which keeps it out of the tree entirely.


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
