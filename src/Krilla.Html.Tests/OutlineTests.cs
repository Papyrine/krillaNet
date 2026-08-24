using Krilla;

/// <summary>
/// The PDF outline, named destinations and document metadata derived from the document itself.
///
/// None of the three shows on a page, so the corpus cannot measure any of them — its comparisons are
/// pixels and box geometry. The per-scenario PDF snapshot would notice a change in the bytes and
/// could not say whether the change was right, which is what these are for: the outline is read back
/// out of the produced PDF, the same way <c>CorpusRunner</c> reads link annotations back.
/// </summary>
public class OutlineTests
{
    const string Nested =
        """
        <!doctype html>
        <html><head><title>A Title</title></head>
        <body>
          <h1>One</h1>
          <h2>One A</h2>
          <h2>One B</h2>
          <h3>One B i</h3>
          <h1>Two</h1>
        </body></html>
        """;

    [Test]
    public async Task HeadingsNestByLevel()
    {
        var outline = Bookmarks(Nested);

        await Assert.That(outline.Select(_ => _.Title)).IsEquivalentTo(["One", "Two"]);

        var one = outline[0];
        await Assert.That(one.Children.Select(_ => _.Title)).IsEquivalentTo(["One A", "One B"]);
        await Assert.That(one.Children[1].Children.Select(_ => _.Title)).IsEquivalentTo(["One B i"]);
    }

    [Test]
    public async Task AGapInLevelsStillNests()
    {
        // h1 then h3 with no h2. Nesting by LEVEL rather than by depth is what makes this a
        // two-level tree; a reading that required consecutive levels would either flatten it or
        // invent an empty h2.
        var outline = Bookmarks(
            """
            <!doctype html>
            <html><body><h1>One</h1><h3>Deep</h3></body></html>
            """);

        await Assert.That(outline.Count).IsEqualTo(1);
        await Assert.That(outline[0].Children.Select(_ => _.Title)).IsEquivalentTo(["Deep"]);
    }

    [Test]
    public async Task ADeeperHeadingBeforeAShallowerOneClosesIt()
    {
        // h2 then h1: the h1 has to become a root rather than a child of the h2 above it.
        var outline = Bookmarks(
            """
            <!doctype html>
            <html><body><h2>First</h2><h1>Second</h1></body></html>
            """);

        await Assert.That(outline.Select(_ => _.Title)).IsEquivalentTo(["First", "Second"]);
    }

    [Test]
    public async Task TheDepthLimitIsByLevel()
    {
        var options = Options();
        options.OutlineDepth = 2;

        var outline = Bookmarks(Nested, options);

        await Assert.That(outline[0].Children.Select(_ => _.Title)).IsEquivalentTo(["One A", "One B"]);

        // The h3 is past the limit and contributes nothing, rather than being promoted to sit
        // beside the h2 it belonged under.
        await Assert.That(outline[0].Children[1].Children).IsEmpty();
    }

    [Test]
    public async Task ZeroDepthProducesNoOutline()
    {
        var options = Options();
        options.OutlineDepth = 0;

        await Assert.That(Bookmarks(Nested, options)).IsEmpty();
    }

    [Test]
    public async Task ADocumentWithNoHeadingsHasNoOutline() =>
        await Assert.That(Bookmarks("<!doctype html><html><body><p>Text</p></body></html>")).IsEmpty();

    [Test]
    public async Task AnEmptyHeadingIsSkipped()
    {
        // A bookmark with no title is an unclickable blank row in a reader, which is worse than
        // one heading missing from the tree.
        var outline = Bookmarks(
            """
            <!doctype html>
            <html><body><h1></h1><h1>Real</h1></body></html>
            """);

        await Assert.That(outline.Select(_ => _.Title)).IsEquivalentTo(["Real"]);
    }

    [Test]
    public async Task AHeadingTitleIsCollapsedToOneLine()
    {
        // The source is indented markup, so the raw text content carries newlines and runs of
        // spaces. A bookmark is a single line in a reader's sidebar.
        var outline = Bookmarks(
            """
            <!doctype html>
            <html><body><h1>
              Two    words
            </h1></body></html>
            """);

        await Assert.That(outline[0].Title).IsEqualTo("Two words");
    }

    [Test]
    public async Task HeadingsPointAtThePageTheyLandedOn()
    {
        // A bookmark is a page and a point on it, so the derivation has to wait for pagination —
        // the same reason a #fragment link does.
        var html =
            """
            <!doctype html>
            <html><head><style>
              html, body { margin: 0; padding: 0; }
              div { height: 1040px; }
            </style></head>
            <body><h1>First</h1><div></div><h1>Second</h1></body></html>
            """;

        var pdf = HtmlConverter.Convert(html, Options());
        using var document = PdfiumDocument.Load(pdf);

        await Assert.That(document.PageCount).IsEqualTo(2);

        var outline = document.GetBookmarks();
        await Assert.That(outline[0].Destination!.Value.PageIndex).IsEqualTo(0);
        await Assert.That(outline[1].Destination!.Value.PageIndex).IsEqualTo(1);
    }

    [Test]
    public async Task TheDocumentTitleBecomesThePdfTitle()
    {
        var pdf = HtmlConverter.Convert(Nested, Options());

        // Read out of the bytes rather than through a metadata API, which Morph.PDFium does not
        // expose. Enough to say the title reached the file, which is the thing that was missing.
        await Assert.That(Encoding.Latin1.GetString(pdf)).Contains("A Title");
    }

    [Test]
    public async Task ACallersTitleWins()
    {
        var options = Options();
        options.Metadata = new() {Title = "Chosen"};

        var pdf = Encoding.Latin1.GetString(HtmlConverter.Convert(Nested, options));

        await Assert.That(pdf).Contains("Chosen");
        await Assert.That(pdf).DoesNotContain("A Title");
    }

    [Test]
    public async Task TheCallersMetadataIsNotMutated()
    {
        // The same object is often reused across conversions, and one document's title leaking into
        // the next would be a memorable bug.
        var metadata = new DocumentMetadata();

        var options = Options();
        options.Metadata = metadata;

        HtmlConverter.Convert(Nested, options);

        await Assert.That(metadata.Title).IsNull();
    }

    [Test]
    public async Task TheDocumentLanguageIsCarriedThrough()
    {
        var pdf = Encoding.Latin1.GetString(HtmlConverter.Convert(
            """
            <!doctype html>
            <html lang="en-GB"><body><p>Text</p></body></html>
            """,
            Options()));

        await Assert.That(pdf).Contains("en-GB");
    }

    [Test]
    public async Task IdsBecomeNamedDestinations()
    {
        var html =
            """
            <!doctype html>
            <html><body><h2 id="introduction">Intro</h2></body></html>
            """;

        var options = Options();
        var withNames = Encoding.Latin1.GetString(HtmlConverter.Convert(html, options));

        options.NamedDestinations = false;
        var without = Encoding.Latin1.GetString(HtmlConverter.Convert(html, options));

        // A named destination is what lets `report.pdf#introduction` open at that heading, so the
        // id has to appear in the file as a name rather than only as layout.
        await Assert.That(withNames).Contains("introduction");
        await Assert.That(without).DoesNotContain("introduction");
    }

    static IReadOnlyList<PdfBookmark> Bookmarks(string html) =>
        Bookmarks(html, Options());

    static IReadOnlyList<PdfBookmark> Bookmarks(string html, HtmlOptions options)
    {
        using var document = PdfiumDocument.Load(HtmlConverter.Convert(html, options));
        return document.GetBookmarks();
    }

    static HtmlOptions Options() =>
        new()
        {
            PageWidth = CorpusLayout.PageWidth,
            PageHeight = CorpusLayout.PageHeight,
            Fonts = CorpusRunner.Options().Fonts
        };
}
