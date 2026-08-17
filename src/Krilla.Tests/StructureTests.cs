/// <summary>
/// Metadata, conformance, outline, links, tagging, graphics and embedding.
/// </summary>
public class StructureTests
{
    [Test]
    public async Task MetadataAppearsInTheOutput()
    {
        using var document = new KrillaDocument();

        document.SetMetadata(
            new()
            {
                Title = "A Test Document",
                Description = "Produced by the Krilla test suite",
                Language = "en-GB",
                Creator = "Krilla.Tests",
                Producer = "Krilla",
                Authors = ["Ada Lovelace"],
                Keywords = ["pdf", "krilla"],
                CreationDate = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero),
                DocumentId = "krilla-test-001"
            });

        using (document.StartPage(200, 200))
        {
        }

        var text = Encoding.Latin1.GetString(document.Finish());

        await Assert.That(text).Contains("A Test Document");
        await Assert.That(text).Contains("Ada Lovelace");
        await Assert.That(text).Contains("Krilla.Tests");
    }

    /// <summary>
    /// Setting a document id and creation date is what makes output byte-reproducible.
    /// </summary>
    [Test]
    public async Task FixedIdAndDateMakeOutputReproducible()
    {
        static byte[] Build()
        {
            using var document = new KrillaDocument();

            document.SetMetadata(
                new()
                {
                    DocumentId = "stable",
                    CreationDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                });

            using (var page = document.StartPage(100, 100))
            {
                page.Surface.FillRectangle(new(10, 10, 90, 90), Color.Rgb(1, 2, 3));
            }

            return document.Finish();
        }

        await Assert.That(Build()).IsEquivalentTo(Build());
    }

    /// <summary>
    /// Output is byte-reproducible even with no metadata set at all.
    /// </summary>
    /// <remarks>
    /// krilla derives the document id from a hash of the content rather than from randomness
    /// or a clock, so identical input yields identical bytes without any opt-in. Setting an
    /// explicit id and creation date pins the value rather than making it stable — worth
    /// knowing, because the opposite is the usual assumption for PDF writers.
    /// </remarks>
    [Test]
    public async Task OutputIsReproducibleWithoutAnyMetadata()
    {
        static byte[] Build()
        {
            using var document = new KrillaDocument();

            using (var page = document.StartPage(100, 100))
            {
                page.Surface.FillRectangle(new(10, 10, 90, 90), Color.Rgb(7, 8, 9));
            }

            return document.Finish();
        }

        await Assert.That(Build()).IsEquivalentTo(Build());
    }

    [Test]
    public async Task OutlineEntriesNest()
    {
        using var document = new KrillaDocument();

        foreach (var _ in Enumerable.Range(0, 3))
        {
            using (document.StartPage(200, 200))
            {
            }
        }

        var chapter = new OutlineItem("Chapter One", 0)
        {
            IsOpen = true
        };
        chapter.Add("Section 1.1", 1);
        chapter.Add("Section 1.2", 2);

        document.SetOutline(chapter, new OutlineItem("Chapter Two", 2));

        var text = Encoding.Latin1.GetString(document.Finish());

        await Assert.That(text).Contains("Chapter One");
        await Assert.That(text).Contains("Section 1.1");
        await Assert.That(text).Contains("Chapter Two");
    }

    [Test]
    public async Task LinksAreWrittenAsAnnotations()
    {
        using var document = new KrillaDocument();

        using (var page = document.StartPage(200, 200))
        {
            page.Surface.AddLink(new(10, 10, 190, 40), "https://example.com/");
            page.Surface.AddLink(new(10, 50, 190, 80), pageIndex: 1);
        }

        using (document.StartPage(200, 200))
        {
        }

        var text = Encoding.Latin1.GetString(document.Finish());

        await Assert.That(text).Contains("/Annots");
        await Assert.That(text).Contains("example.com");
    }

    [Test]
    public async Task NamedDestinationsAreRegistered()
    {
        using var document = new KrillaDocument();

        using (document.StartPage(200, 200))
        {
        }

        document.RegisterDestination("intro", 0, new(0, 0));

        var text = Encoding.Latin1.GetString(document.Finish());
        await Assert.That(text).Contains("intro");
    }

    [Test]
    public async Task DuplicateNamedDestinationThrows()
    {
        using var document = new KrillaDocument();

        using (document.StartPage(200, 200))
        {
        }

        document.RegisterDestination("target", 0, new(0, 0));

        await Assert.That(() => document.RegisterDestination("target", 0, new(50, 50)))
            .Throws<KrillaException>();
    }

    [Test]
    public async Task AGraphicIsEmittedOnceAndReferenced()
    {
        using var document = new KrillaDocument();
        var repeated = 0;
        var single = 0;

        using (var page = document.StartPage(300, 300))
        {
            var graphic = page.Surface.CaptureGraphic(
                surface => surface.FillRectangle(new(0, 0, 40, 40), Color.Rgb(200, 30, 30)));

            using (graphic)
            {
                for (var index = 0; index < 20; index++)
                {
                    using (page.Surface.PushTransform(Matrix.Translate(index * 10, index * 10)))
                    {
                        page.Surface.DrawGraphic(graphic);
                    }
                }
            }
        }

        repeated = document.Finish().Length;

        using var comparison = new KrillaDocument();

        using (var page = comparison.StartPage(300, 300))
        {
            using var graphic = page.Surface.CaptureGraphic(
                surface => surface.FillRectangle(new(0, 0, 40, 40), Color.Rgb(200, 30, 30)));

            page.Surface.DrawGraphic(graphic);
        }

        single = comparison.Finish().Length;

        // Twenty placements cost barely more than one, because only the transforms repeat.
        await Assert.That(repeated).IsLessThan(single * 2);
    }

    [Test]
    public async Task AGraphicFromAnotherDocumentThrows()
    {
        using var first = new KrillaDocument();
        using var page = first.StartPage(100, 100);

        using var graphic = page.Surface.CaptureGraphic(
            surface => surface.FillRectangle(new(0, 0, 10, 10), Color.Black));

        using var second = new KrillaDocument();
        using var secondPage = second.StartPage(100, 100);

        // krilla cannot detect this itself and would emit a PDF referencing objects that do
        // not exist.
        await Assert.That(() => secondPage.Surface.DrawGraphic(graphic)).Throws<KrillaException>();
    }

    [Test]
    public async Task PatternsAndMasksProduceAValidDocument()
    {
        using var document = new KrillaDocument();

        using (var page = document.StartPage(200, 200))
        {
            var surface = page.Surface;

            using var pattern = surface.CapturePattern(
                tile => tile.FillRectangle(new(0, 0, 10, 10), Color.Rgb(40, 120, 200)),
                width: 20,
                height: 20);

            using (surface.PushMask(
                       mask => mask.FillRectangle(new(50, 50, 150, 150), Color.White)))
            {
                using var path = PdfPath.Rectangle(new(0, 0, 200, 200));
                surface.SetFill(pattern).DrawPath(path);
            }
        }

        var pdf = document.Finish();
        await Assert.That(Encoding.ASCII.GetString(pdf, 0, 5)).IsEqualTo("%PDF-");
    }

    [Test]
    public async Task EmbeddedFilesAreAttached()
    {
        using var document = new KrillaDocument();

        using (document.StartPage(200, 200))
        {
        }

        document.EmbedFile(
            "notes.txt",
            "some embedded data"u8,
            mimeType: "text/plain",
            description: "Notes",
            association: FileAssociation.Supplement);

        var text = Encoding.Latin1.GetString(document.Finish());

        await Assert.That(text).Contains("notes.txt");
        await Assert.That(text).Contains("EmbeddedFile");
    }

    [Test]
    public async Task EmbeddingTheSamePathTwiceThrows()
    {
        using var document = new KrillaDocument();

        using (document.StartPage(200, 200))
        {
        }

        document.EmbedFile("data.bin", "one"u8);

        await Assert.That(() => document.EmbedFile("data.bin", "two"u8))
            .Throws<KrillaException>();
    }

    [Test]
    public async Task PagesFromAnExistingPdfCanBeReused()
    {
        // Produce a source document, then consume it back.
        byte[] source;

        using (var first = new KrillaDocument())
        {
            using (var page = first.StartPage(100, 100))
            {
                page.Surface.FillRectangle(new(20, 20, 80, 80), Color.Rgb(0, 140, 70));
            }

            source = first.Finish();
        }

        using var pdf = PdfSource.Load(source);
        await Assert.That(pdf.PageCount).IsEqualTo(1);

        using var document = new KrillaDocument();

        using (var page = document.StartPage(200, 200))
        {
            page.Surface.DrawPdfPage(pdf, 0, new(50, 50, 150, 150));
        }

        var result = document.Finish();
        await Assert.That(Encoding.ASCII.GetString(result, 0, 5)).IsEqualTo("%PDF-");
    }

    [Test]
    public async Task InvalidPdfDataThrows() =>
        await Assert.That(() => PdfSource.Load("not a pdf"u8)).Throws<KrillaException>();

    [Test]
    public async Task ATaggedDocumentCarriesAStructureTree()
    {
        using var document = new KrillaDocument(
            new()
            {
                EnableTagging = true
            });

        TagIdentifier heading;
        TagIdentifier body;

        using (var page = document.StartPage(300, 200))
        {
            var surface = page.Surface;

            heading = surface.BeginText(language: "en-GB");
            surface.FillRectangle(new(20, 20, 280, 40), Color.Rgb(20, 20, 20));
            surface.EndTagged();

            body = surface.BeginText();
            surface.FillRectangle(new(20, 60, 280, 160), Color.Gray(120));
            surface.EndTagged();
        }

        using var tree = new TagTree();
        tree.WithLanguage("en-GB");

        var section = tree.Add(TagKind.Section);
        section.Add(Tag.Heading(1, "The Heading")).Add(heading);
        section.Add(TagKind.Paragraph).Add(body);

        document.SetTagTree(tree);

        var text = Encoding.Latin1.GetString(document.Finish());

        await Assert.That(text).Contains("StructTreeRoot");
        await Assert.That(text).Contains("The Heading");
    }

    [Test]
    public async Task ATaggedTableCarriesSpansAndHeaders()
    {
        using var document = new KrillaDocument(
            new()
            {
                EnableTagging = true
            });

        TagIdentifier headerCell;
        TagIdentifier dataCell;

        using (var page = document.StartPage(300, 200))
        {
            var surface = page.Surface;

            headerCell = surface.BeginText();
            surface.FillRectangle(new(20, 20, 140, 50), Color.Gray(200));
            surface.EndTagged();

            dataCell = surface.BeginText();
            surface.FillRectangle(new(20, 60, 140, 90), Color.White);
            surface.EndTagged();
        }

        using var tree = new TagTree();
        var table = tree.Add(TagKind.Table);
        table.WithSummary("A one-column table");

        var headerRow = table.Add(TagKind.TableRow);
        headerRow.Add(Tag.TableHeader(TableHeaderScope.Column).WithId("col1")).Add(headerCell);

        var bodyRow = table.Add(TagKind.TableRow);
        bodyRow.Add(Tag.Create(TagKind.TableCell).WithHeaders("col1").WithColumnSpan(1)).Add(dataCell);

        document.SetTagTree(tree);

        var text = Encoding.Latin1.GetString(document.Finish());
        await Assert.That(text).Contains("StructTreeRoot");
    }

    [Test]
    public async Task ArtifactsAreExcludedFromTheStructure()
    {
        using var document = new KrillaDocument(
            new()
            {
                EnableTagging = true
            });

        using (var page = document.StartPage(200, 200))
        {
            var surface = page.Surface;

            // A running head is decoration, not content, so it never enters the tree.
            surface.BeginArtifact(ArtifactKind.Header);
            surface.FillRectangle(new(0, 0, 200, 20), Color.Gray(220));
            surface.EndTagged();
        }

        using var tree = new TagTree();
        document.SetTagTree(tree);

        var pdf = document.Finish();
        await Assert.That(Encoding.ASCII.GetString(pdf, 0, 5)).IsEqualTo("%PDF-");
    }

    /// <summary>
    /// Conformance violations surface when the document is finished, not when the offending
    /// content is added.
    /// </summary>
    /// <remarks>
    /// PDF/A requires an ICC profile for CMYK content, and krilla ships none by default.
    /// Violations are batched and reported together at the end, so a document is checked as a
    /// whole rather than call by call.
    /// </remarks>
    [Test]
    public async Task PdfAConformanceIsEnforcedAtFinish()
    {
        using var document = new KrillaDocument(
            new()
            {
                Archival = PdfArchival.A2B,
                XmpMetadata = true
            });

        using (var page = document.StartPage(100, 100))
        {
            page.Surface.FillRectangle(new(10, 10, 90, 90), Color.Cmyk(20, 40, 0, 10));
        }

        await Assert.That(document.Finish).Throws<KrillaException>();
    }

    /// <summary>
    /// The same document in RGB finishes cleanly, so the test above is asserting the CMYK
    /// profile requirement rather than PDF/A being unusable.
    /// </summary>
    [Test]
    public async Task PdfAAcceptsAConformingDocument()
    {
        using var document = new KrillaDocument(
            new()
            {
                Archival = PdfArchival.A2B,
                XmpMetadata = true
            });

        using (var page = document.StartPage(100, 100))
        {
            page.Surface.FillRectangle(new(10, 10, 90, 90), Color.Rgb(20, 40, 60));
        }

        var pdf = document.Finish();
        await Assert.That(Encoding.ASCII.GetString(pdf, 0, 5)).IsEqualTo("%PDF-");
    }

    // PDF/A-4 needs PDF 2.0; asking for it against 1.4 is rejected at construction.
    [Test]
    public async Task AnUnsupportedConformanceCombinationThrows() =>
        await Assert.That(
                () => new KrillaDocument(
                    new()
                    {
                        Version = PdfVersion.Pdf14,
                        Archival = PdfArchival.A4
                    }))
            .Throws<KrillaException>();
}
