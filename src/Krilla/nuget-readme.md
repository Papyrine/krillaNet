# Krilla

A .NET wrapper over [krilla](https://github.com/LaurenzV/krilla), the Rust PDF-writing library that backs [typst](https://typst.app). Creates PDF documents: pages, vector paths, gradients, text, raster images and SVG. The native library ships inside the package for Windows, Linux (glibc and musl) and macOS, on x64 and arm64.

```cs
using var document = new KrillaDocument();

using (var page = document.StartPage(PageSettings.A4))
{
    page.Surface.FillRectangle(
        Rectangle.FromSize(50, 50, 200, 100),
        Color.Rgb(220, 40, 40));
}

document.Save("output.pdf");
```

Krilla *writes* PDFs. To read, render or edit an existing one, use [Morph.PDFium](https://www.nuget.org/packages/Morph.PDFium/).

See the [documentation](https://github.com/Papyrine/krillaNet) for the full API.
