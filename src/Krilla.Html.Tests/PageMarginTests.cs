/// <summary>
/// Page margins, which the corpus cannot measure.
///
/// Every scenario runs with all four margins at zero, deliberately: that makes the page content
/// box and the browser viewport the same rectangle, which is what lets the root element's box be
/// compared directly. So the corpus is blind to <see cref="HtmlOptions.MarginBottom"/> and its
/// three siblings, and a break in them would show up nowhere — hence these.
///
/// The bottom margin is the awkward one. The other three move content, so getting one wrong is
/// visible immediately; the bottom margin moves nothing and only takes height away, so a
/// regression presents as a page count that is quietly one too low with content missing off the
/// end of each page.
/// </summary>
public class PageMarginTests
{
    // Ten 100px blocks: 1000px of content, which fits US Letter's 1056px page exactly once and
    // does not fit any page shortened by more than 56px.
    const string Tall =
        """
        <!doctype html>
        <html><head><style>
          html, body { margin: 0; padding: 0; }
          div { height: 100px; background: #c81e1e; }
        </style></head>
        <body>
          <div></div><div></div><div></div><div></div><div></div>
          <div></div><div></div><div></div><div></div><div></div>
        </body></html>
        """;

    // One long paragraph, so the number of lines it takes is a function of the content width.
    const string Paragraph =
        """
        <!doctype html>
        <html><head><style>
          html, body { margin: 0; padding: 0; }
          p { margin: 0; font-family: sans-serif; font-size: 16px; color: #c81e1e; }
        </style></head>
        <body><p>
          The quick brown fox jumps over the lazy dog, and then does it again, and again, and
          again, until the sentence is long enough to wrap several times over on any page width
          this test is likely to hand it.
        </p></body></html>
        """;

    [Test]
    public async Task BottomMarginShrinksTheContentArea()
    {
        await Assert.That(PageCount(Options())).IsEqualTo(1);

        var shortened = Options();
        shortened.MarginBottom = 200;

        // 1056 - 200 leaves 856, so the ninth block no longer fits and moves overleaf.
        await Assert.That(PageCount(shortened)).IsEqualTo(2);
    }

    [Test]
    public async Task BottomMarginCostsTheSameHeightAsTopMargin()
    {
        // Both come out of ContentHeight, so the same amount on either edge has to paginate the
        // same document identically. Subtracting one and not the other is the likely defect, and
        // it is invisible whenever only one of them is set.
        var top = Options();
        top.MarginTop = 300;

        var bottom = Options();
        bottom.MarginBottom = 300;

        var both = Options();
        both.MarginTop = 150;
        both.MarginBottom = 150;

        var expected = PageCount(top);
        await Assert.That(expected).IsGreaterThan(1);
        await Assert.That(PageCount(bottom)).IsEqualTo(expected);
        await Assert.That(PageCount(both)).IsEqualTo(expected);
    }

    [Test]
    public async Task NothingPaintsBelowTheBottomMargin()
    {
        var options = Options();
        options.MarginBottom = 200;

        var page = Render(options, 0);

        // The band between the content edge and the paper edge is the margin, and it must be
        // blank. Content painted into it is what a bottom margin subtracted from the layout but
        // not from the painted area looks like: the page count is right and the ink runs on past
        // where it was supposed to stop.
        var lastInked = LastInkedRow(page);
        await Assert.That(lastInked).IsLessThan(CorpusLayout.PageHeight - 200);

        // And the content area itself is used rather than left short — the block right above the
        // margin has to be there, or this test would pass on a blank page.
        await Assert.That(lastInked).IsGreaterThan(CorpusLayout.PageHeight - 200 - 110);
    }

    [Test]
    public async Task TopAndLeftMarginsOffsetTheContent()
    {
        var options = Options();
        options.MarginTop = 100;
        options.MarginLeft = 50;

        var page = Render(options, 0);

        await Assert.That(FirstInkedRow(page)).IsEqualTo(100);
        await Assert.That(FirstInkedColumn(page)).IsEqualTo(50);
    }

    [Test]
    public async Task RightMarginShrinksTheContentWidth()
    {
        var options = Options();
        options.MarginRight = 200;

        var page = Render(options, 0);

        // The blocks are auto width, so they fill the content box exactly — the last inked column
        // is the content edge, and it has to sit inside the margin.
        var lastInked = LastInkedColumn(page);
        await Assert.That(lastInked).IsLessThan(CorpusLayout.PageWidth - 200);

        // And the content area is used rather than left short. Without this the test would pass
        // on a page whose blocks had collapsed to nothing.
        await Assert.That(lastInked).IsGreaterThan(CorpusLayout.PageWidth - 200 - 3);

        // The right margin moves nothing: content still starts at the left paper edge.
        await Assert.That(FirstInkedColumn(page)).IsEqualTo(0);
    }

    [Test]
    public async Task RightMarginCostsTheSameWidthAsLeftMargin()
    {
        // Both come out of ContentWidth, so the same amount on either edge has to produce the
        // same content width. Subtracting one and not the other is the likely defect, and it is
        // invisible whenever only the left one is set — that one also offsets the content, so a
        // width bug there hides behind a position change that looks right.
        var left = Options();
        left.MarginLeft = 300;

        var right = Options();
        right.MarginRight = 300;

        var both = Options();
        both.MarginLeft = 150;
        both.MarginRight = 150;

        await Assert.That(InkedWidth(Render(left, 0))).IsEqualTo(InkedWidth(Render(right, 0)));
        await Assert.That(InkedWidth(Render(both, 0))).IsEqualTo(InkedWidth(Render(right, 0)));
    }

    [Test]
    public async Task RightMarginWrapsTextEarlier()
    {
        // A width the paint tests cannot see: the narrowed content box has to reach layout, not
        // just clip the painting. One long line wrapped against the shortened width takes more
        // lines, so the page gets taller — measured as the last inked row rather than the width.
        var wide = Options();
        var narrow = Options();
        narrow.MarginRight = 500;

        await Assert.That(LastInkedRow(Render(narrow, 0, Paragraph)))
            .IsGreaterThan(LastInkedRow(Render(wide, 0, Paragraph)));
    }

    [Test]
    public async Task WithMarginSetsAllFourEdges()
    {
        var options = HtmlOptions.Letter.WithMargin(72);

        await Assert.That(options.MarginTop).IsEqualTo(72);
        await Assert.That(options.MarginRight).IsEqualTo(72);
        await Assert.That(options.MarginBottom).IsEqualTo(72);
        await Assert.That(options.MarginLeft).IsEqualTo(72);

        // The two the rest of the engine actually reads. Setting a field and not subtracting it
        // is the failure this catches.
        await Assert.That(options.ContentWidth).IsEqualTo(options.PageWidth - 144);
        await Assert.That(options.ContentHeight).IsEqualTo(options.PageHeight - 144);
    }

    [Test]
    public async Task WithMarginMutatesRatherThanCopying()
    {
        // It reads like a with-er and is not one: it returns the same instance, so a caller
        // holding the original sees the change. Worth pinning, because the fluent shape invites
        // the opposite assumption.
        var options = Options();
        var returned = options.WithMargin(30);

        await Assert.That(returned).IsSameReferenceAs(options);
        await Assert.That(options.MarginLeft).IsEqualTo(30);
    }

    [Test]
    public async Task WithMarginOfZeroClearsExistingMargins()
    {
        var options = Options().WithMargin(50);

        await Assert.That(options.WithMargin(0).ContentWidth).IsEqualTo(options.PageWidth);
    }

    [Test]
    public async Task WithMarginInsetsAllFourEdges()
    {
        var page = Render(Options().WithMargin(100), 0);

        await Assert.That(FirstInkedRow(page)).IsEqualTo(100);
        await Assert.That(FirstInkedColumn(page)).IsEqualTo(100);

        var lastColumn = LastInkedColumn(page);
        await Assert.That(lastColumn).IsLessThan(CorpusLayout.PageWidth - 100);
        await Assert.That(lastColumn).IsGreaterThan(CorpusLayout.PageWidth - 100 - 3);

        // The bottom edge only bounds the content; a page filled to the break can stop short of
        // it, so this is a ceiling rather than an equality.
        await Assert.That(LastInkedRow(page)).IsLessThan(CorpusLayout.PageHeight - 100);
    }

    static HtmlOptions Options() =>
        new()
        {
            PageWidth = CorpusLayout.PageWidth,
            PageHeight = CorpusLayout.PageHeight,
            Fonts = CorpusRunner.Options().Fonts
        };

    static int PageCount(HtmlOptions options)
    {
        using var document = PdfiumDocument.Load(HtmlConverter.Convert(Tall, options));
        return document.PageCount;
    }

    static PngImage Render(HtmlOptions options, int index) =>
        Render(options, index, Tall);

    static PngImage Render(HtmlOptions options, int index, string html)
    {
        using var document = PdfiumDocument.Load(HtmlConverter.Convert(html, options));
        var png = document.RenderPage(index, new RenderOptions
        {
            Dpi = CorpusLayout.Dpi
        });

        using var stream = new MemoryStream(png);
        return PngDecoder.Decode(stream);
    }

    static int LastInkedRow(PngImage image)
    {
        for (var y = image.Height - 1; y >= 0; y--)
        {
            if (RowIsInked(image, y))
            {
                return y;
            }
        }

        return -1;
    }

    static int FirstInkedRow(PngImage image)
    {
        for (var y = 0; y < image.Height; y++)
        {
            if (RowIsInked(image, y))
            {
                return y;
            }
        }

        return -1;
    }

    static int FirstInkedColumn(PngImage image)
    {
        for (var x = 0; x < image.Width; x++)
        {
            for (var y = 0; y < image.Height; y++)
            {
                if (IsInked(image, x, y))
                {
                    return x;
                }
            }
        }

        return -1;
    }

    static int LastInkedColumn(PngImage image)
    {
        for (var x = image.Width - 1; x >= 0; x--)
        {
            if (ColumnIsInked(image, x))
            {
                return x;
            }
        }

        return -1;
    }

    static int InkedWidth(PngImage image) =>
        LastInkedColumn(image) - FirstInkedColumn(image);

    static bool ColumnIsInked(PngImage image, int x)
    {
        for (var y = 0; y < image.Height; y++)
        {
            if (IsInked(image, x, y))
            {
                return true;
            }
        }

        return false;
    }

    static bool RowIsInked(PngImage image, int y)
    {
        for (var x = 0; x < image.Width; x++)
        {
            if (IsInked(image, x, y))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The blocks are a saturated red on white, so anything that is not near-white is content.
    /// A loose threshold on purpose: the edge of a filled rectangle is antialiased, and demanding
    /// the exact colour would make the answer depend on where that edge landed.
    /// </summary>
    static bool IsInked(PngImage image, int x, int y)
    {
        var offset = (y * image.Width + x) * 4;
        var rgba = image.Rgba;
        return rgba[offset] < 240 || rgba[offset + 1] < 240 || rgba[offset + 2] < 240;
    }
}
