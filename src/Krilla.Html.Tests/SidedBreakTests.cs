/// <summary>
/// Forced breaks that name the SHEET they must land on, which the corpus cannot measure.
///
/// The whole of what distinguishes them from <c>always</c> is a blank page, so what wants asserting
/// is a page COUNT and which pages carry ink — neither of which a scenario comparing one page
/// against one reference image can say.
///
/// Only <c>left</c> and <c>right</c> are tested, because they are the only two that arrive.
/// AngleSharp accepts <c>always</c>, <c>left</c>, <c>right</c> and <c>avoid</c> on
/// <c>page-break-before</c> and adds <c>page</c> on <c>break-before</c>, and drops <c>recto</c> and
/// <c>verso</c> from both — so those two are unreachable and, like <c>revert</c>, cannot even be
/// reported. The resolver maps them anyway, since a value that costs one line to accept should not
/// need a second visit when the parser learns it.
/// </summary>
public class SidedBreakTests
{
    static string Document(string value) =>
        """
        <!doctype html>
        <html><head><style>
          html, body { margin: 0; padding: 0; }
          div { height: 40px; background: #c81e1e; }
          #second { page-break-before: VALUE; }
        </style></head>
        <body><div id="first"></div><div id="second"></div></body></html>
        """.Replace("VALUE", value);

    [Test]
    public async Task AlwaysTakesTwoPages() =>
        // The control. Both boxes are 40px on a 1056px page, so nothing but the break makes a
        // second page at all.
        await Assert.That(await PageCount("always")).IsEqualTo(2);

    [Test]
    public async Task LeftInsertsNothingWhenThePageIsAlreadyEven() =>
        // A LEFT-hand sheet is an even page. The break puts the box on page two, which is even
        // already, so nothing is inserted — and that is the case worth asserting, because a
        // reading that always inserted a page would look like it was working on `right` alone.
        await Assert.That(await PageCount("left")).IsEqualTo(2);

    [Test]
    public async Task RightInsertsABlankPage() =>
        // A RIGHT-hand sheet is an odd page. The break lands on page two, so a blank page two is
        // inserted and the content goes to page three.
        await Assert.That(await PageCount("right")).IsEqualTo(3);

    [Test]
    public async Task TheInsertedPageIsBlank()
    {
        var pdf = await HtmlConverter.ConvertAsync(Document("right"), Options());
        using var document = PdfiumDocument.Load(pdf);

        await Assert.That(document.PageCount).IsEqualTo(3);

        // Page one carries the first box, page two nothing at all, page three the second box. A
        // blank page whose slice runs from a position to itself holds the canvas and no content,
        // which is what a blank page in a browser holds too.
        await Assert.That(Inked(document, 0)).IsTrue();
        await Assert.That(Inked(document, 1)).IsFalse();
        await Assert.That(Inked(document, 2)).IsTrue();
    }

    [Test]
    public async Task ABreakAtTheStartOfTheDocumentIsStillDropped()
    {
        // The rule that keeps `page-break-before` on a section wrapper from opening every document
        // with a blank page applies to the sided values too — otherwise `right` on the first
        // element would produce a blank page one and land the content on an even page anyway.
        var html =
            """
            <!doctype html>
            <html><head><style>
              html, body { margin: 0; padding: 0; }
              div { height: 40px; background: #c81e1e; page-break-before: right; }
            </style></head>
            <body><div></div></body></html>
            """;

        using var document = PdfiumDocument.Load(await HtmlConverter.ConvertAsync(html, Options()));

        await Assert.That(document.PageCount).IsEqualTo(1);
    }

    static async Task<int> PageCount(string value)
    {
        using var document = PdfiumDocument.Load(await HtmlConverter.ConvertAsync(Document(value), Options()));
        return document.PageCount;
    }

    static bool Inked(PdfiumDocument document, int index)
    {
        var png = document.RenderPage(index, new RenderOptions {Dpi = CorpusLayout.Dpi});

        using var stream = new MemoryStream(png);
        var image = PngDecoder.Decode(stream);

        for (var offset = 0; offset < image.Rgba.Length; offset += 4)
        {
            if (image.Rgba[offset] > 120 &&
                image.Rgba[offset + 1] < 120 &&
                image.Rgba[offset + 2] < 120)
            {
                return true;
            }
        }

        return false;
    }

    static HtmlOptions Options() =>
        new()
        {
            PageWidth = CorpusLayout.PageWidth,
            PageHeight = CorpusLayout.PageHeight,
            Fonts = CorpusRunner.Options().Fonts
        };
}
