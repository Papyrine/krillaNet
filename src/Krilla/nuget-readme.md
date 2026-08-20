# Krilla

A .NET wrapper over [krilla](https://github.com/LaurenzV/krilla), the Rust PDF-writing library that backs [typst](https://typst.app). Creates PDF documents: pages, vector paths, gradients, text, and images. The native library ships inside the package for Windows, Linux (glibc and musl) and macOS, on x64 and arm64.

<!-- snippet: SaveToFile -->
<a id='snippet-SaveToFile'></a>
```cs
using var document = new KrillaDocument();

using (var page = document.StartPage(PageSettings.A4))
{
    page.Surface.FillRectangle(
        Rectangle.FromSize(50, 50, 200, 100),
        Color.Rgb(220, 40, 40));
}

document.Save(path);
```
<sup><a href='/src/Krilla.Tests/Samples.cs#L459-L472' title='Snippet source file'>snippet source</a> | <a href='#snippet-SaveToFile' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Krilla *writes* PDFs. To read, render or edit an existing one, use [Morph.PDFium](https://www.nuget.org/packages/Morph.PDFium/).

Converting HTML instead? See [Krilla.Html](https://www.nuget.org/packages/Krilla.Html/).

See the [documentation](https://github.com/Papyrine/krillaNet) for the full API.
