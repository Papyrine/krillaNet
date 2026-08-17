/// <summary>
/// Tests that double as the readme's usage snippets, via MarkdownSnippets.
/// </summary>
public class Samples
{
    [Test]
    public void HelloWorld()
    {
        #region HelloWorld

        using var document = new KrillaDocument();

        using (var page = document.StartPage(PageSettings.A4))
        {
            page.Surface.FillRectangle(
                Rectangle.FromSize(50, 50, 200, 100),
                Color.Rgb(220, 40, 40));
        }

        var pdf = document.Finish();

        #endregion

        AssertPdf(pdf);
    }

    [Test]
    public void DrawAPath()
    {
        #region DrawAPath

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

        #endregion

        AssertPdf(pdf);
    }

    [Test]
    public void FillAndStroke()
    {
        #region FillAndStroke

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

        #endregion

        AssertPdf(pdf);
    }

    [Test]
    public void GradientFill()
    {
        #region GradientFill

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

        #endregion

        AssertPdf(pdf);
    }

    [Test]
    public void TransformsAndOpacity()
    {
        #region TransformsAndOpacity

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

        #endregion

        AssertPdf(pdf);
    }

    [Test]
    public void MultiplePages()
    {
        #region MultiplePages

        using var document = new KrillaDocument();

        foreach (var index in Enumerable.Range(0, 3))
        {
            using var page = document.StartPage(PageSettings.Letter);
            page.Surface.FillRectangle(
                Rectangle.FromSize(72, 72 * (index + 1), 200, 40),
                Color.Gray(80));
        }

        document.Save(Path.Combine(Path.GetTempPath(), "krilla-sample.pdf"));

        #endregion

        File.Delete(Path.Combine(Path.GetTempPath(), "krilla-sample.pdf"));
    }

    [Test]
    public void DrawAnImage()
    {
        #region DrawAnImage

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

        #endregion

        AssertPdf(pdf);
    }

    [Test]
    public void Metadata()
    {
        #region Metadata

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

        #endregion

        AssertPdf(pdf);
    }

    [Test]
    public void Bookmarks()
    {
        #region Bookmarks

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

        #endregion

        AssertPdf(pdf);
    }

    [Test]
    public void Links()
    {
        #region Links

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

        #endregion

        AssertPdf(pdf);
    }

    [Test]
    public void ArchivalPdf()
    {
        #region ArchivalPdf

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

        #endregion

        AssertPdf(pdf);
    }

    [Test]
    public void AccessibleDocument()
    {
        #region AccessibleDocument

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

        #endregion

        AssertPdf(pdf);
    }

    [Test]
    public void ReusableGraphic()
    {
        #region ReusableGraphic

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

        #endregion

        AssertPdf(pdf);
    }

    [Test]
    public void Attachments()
    {
        #region Attachments

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

        #endregion

        AssertPdf(pdf);
    }

    static void AssertPdf(byte[] pdf)
    {
        if (pdf.Length < 100 ||
            !"%PDF-"u8.SequenceEqual(pdf.AsSpan(0, 5)))
        {
            throw new("Expected a PDF document.");
        }
    }
}
