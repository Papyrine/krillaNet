/// <summary>
/// <c>orphans</c> and <c>widows</c>, which the corpus cannot measure because the reference browser
/// does not implement them.
///
/// That is not a guess. <c>page/break_between_lines</c> holds a Chromium reference in which a
/// three-line paragraph is broken after its second line, leaving one line overleaf under the
/// initial <c>widows: 2</c> — so the browser ignored the property. Honouring it by default would
/// therefore make this engine disagree with the reference on every document long enough to
/// paginate, which is why <see cref="HtmlOptions.HonourOrphansAndWidows"/> exists and why it is
/// off.
///
/// So the property is asserted here instead, by page count and by where the ink falls, on the same
/// arrangement the corpus scenario uses.
/// </summary>
public class RunConstraintTests
{
    /// <summary>
    /// The arrangement `page/break_between_lines` uses: a spacer leaving room for two of the
    /// paragraph's four lines.
    /// </summary>
    /// <remarks>
    /// 980px of spacer on a 1056px page leaves 76px, which holds two 32px lines and not three, so
    /// the unconstrained break falls after the second. Under the initial <c>widows: 2</c> that is
    /// already legal — two above and two below — which is why the test that exercises a violation
    /// declares its own counts rather than relying on the defaults.
    /// </remarks>
    const string Straddling =
        """
        <!doctype html>
        <html><head><style>
          html, body { margin: 0; padding: 0; }
          #spacer { height: 980px; }
          p { width: 600px; margin: 0; font-size: 20px; line-height: 32px; color: #c81e1e; }
        </style></head>
        <body>
          <div id="spacer"></div>
          <p>This paragraph starts near the bottom of the first page and has enough text to run
          past the boundary, so the break has to fall between two of its lines rather than through
          one of them.</p>
        </body></html>
        """;

    [Test]
    public async Task UnconstrainedTheBreakFallsBetweenLines()
    {
        // The control, and the behaviour the corpus reference agrees with: two lines stay, one
        // goes overleaf.
        var first = Render(Straddling, Options(), 0);

        await Assert.That(LastInkedRow(first)).IsGreaterThan(1010);
    }

    [Test]
    public async Task TheDefaultCountsAreAlreadySatisfiedHere()
    {
        // Two lines above the break and two below, which `orphans: 2; widows: 2` permits — so
        // turning the constraint on must change nothing. Worth asserting: a reading that compared
        // the counts the wrong way round would move this run and look like it was working.
        var options = Options();
        options.HonourOrphansAndWidows = true;

        await Assert.That(LastInkedRow(Render(Straddling, options, 0)))
            .IsEqualTo(LastInkedRow(Render(Straddling, Options(), 0)));
    }

    [Test]
    public async Task NeitherConstraintSatisfiableMovesTheWholeRun()
    {
        var options = Options();
        options.HonourOrphansAndWidows = true;

        // Three of each on a four-line run: two above the break is one short of `orphans`, and
        // moving the break to leave three below would leave only one above. Nothing satisfies both,
        // so the whole run goes overleaf — which is what a print engine does with a short paragraph
        // caught at a page edge.
        var html = Straddling.Replace(
            "line-height: 32px;",
            "line-height: 32px; orphans: 3; widows: 3;");

        await Assert.That(LastInkedRow(Render(html, options, 0))).IsEqualTo(-1);
    }

    [Test]
    public async Task TheRunStillPaginatesRatherThanLooping()
    {
        // The guard that matters most: moving a run earlier must always advance the page top, or
        // the loop that produces page tops never terminates. A paragraph taller than the page it
        // would move to is the case that tests it.
        var options = Options();
        options.HonourOrphansAndWidows = true;

        var tall =
            """
            <!doctype html>
            <html><head><style>
              html, body { margin: 0; padding: 0; }
              #spacer { height: 1000px; }
              p { margin: 0; font-size: 20px; line-height: 32px; color: #c81e1e; }
            </style></head>
            <body><div id="spacer"></div><p>
            """ +
            string.Join(" ", Enumerable.Repeat("The quick brown fox jumps over the lazy dog.", 80)) +
            "</p></body></html>";

        using var document = PdfiumDocument.Load(HtmlConverter.Convert(tall, options));

        await Assert.That(document.PageCount).IsGreaterThan(1);
        await Assert.That(document.PageCount).IsLessThan(10);
    }

    [Test]
    public async Task ALongRunKeepsTheBreakAndOnlyMovesIt()
    {
        // A run long enough to satisfy both constraints must NOT move whole — that would turn a
        // typographic nicety into a page of white space. It moves the break instead, by however
        // many lines the widow count asks for.
        var options = Options();
        options.HonourOrphansAndWidows = true;

        var html =
            """
            <!doctype html>
            <html><head><style>
              html, body { margin: 0; padding: 0; }
              #spacer { height: 700px; }
              p { width: 600px; margin: 0; font-size: 20px; line-height: 32px; color: #c81e1e;
                  widows: 4; }
            </style></head>
            <body><div id="spacer"></div><p>
            """ +
            string.Join(" ", Enumerable.Repeat("The quick brown fox jumps over the lazy dog.", 12)) +
            "</p></body></html>";

        var first = Render(html, options, 0);

        // Content still reaches the first page — the run was not moved whole — and it stops
        // earlier than the page edge, because four lines were carried over instead of two.
        await Assert.That(LastInkedRow(first)).IsGreaterThan(700);
        await Assert.That(LastInkedRow(first)).IsLessThan(1056 - 32);
    }

    [Test]
    public async Task ASingleLineParagraphIsUnaffected()
    {
        // A run of one line can never be split, so neither constraint applies to it — and a naive
        // reading that compared the line count to `orphans` would move every short paragraph that
        // happened to straddle a boundary.
        var options = Options();
        options.HonourOrphansAndWidows = true;

        var html =
            """
            <!doctype html>
            <html><head><style>
              html, body { margin: 0; padding: 0; }
              div { height: 1040px; }
              p { margin: 0; font-size: 20px; line-height: 32px; color: #c81e1e; }
            </style></head>
            <body><div></div><p>One line.</p></body></html>
            """;

        using var document = PdfiumDocument.Load(HtmlConverter.Convert(html, options));

        // The line does not fit under the 1040px block, so it moves to the second page as one
        // unbreakable unit — two pages, not three, and no attempt to constrain a run of one.
        await Assert.That(document.PageCount).IsEqualTo(2);
    }

    static HtmlOptions Options() =>
        new()
        {
            PageWidth = CorpusLayout.PageWidth,
            PageHeight = CorpusLayout.PageHeight,
            Fonts = CorpusRunner.Options().Fonts
        };

    static PngImage Render(string html, HtmlOptions options, int index)
    {
        using var document = PdfiumDocument.Load(HtmlConverter.Convert(html, options));
        var png = document.RenderPage(index, new RenderOptions {Dpi = CorpusLayout.Dpi});

        using var stream = new MemoryStream(png);
        return PngDecoder.Decode(stream);
    }

    /// <summary>
    /// The last row carrying the paragraph's deliberate red, or -1 when the page holds none of it.
    /// </summary>
    /// <remarks>
    /// Keyed on the colour rather than on "not white", so the spacer — which paints nothing but
    /// still occupies the page — cannot be mistaken for content.
    /// </remarks>
    static int LastInkedRow(PngImage image)
    {
        for (var y = image.Height - 1; y >= 0; y--)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var offset = (y * image.Width + x) * 4;
                var rgba = image.Rgba;

                if (rgba[offset] > 120 && rgba[offset + 1] < 120 && rgba[offset + 2] < 120)
                {
                    return y;
                }
            }
        }

        return -1;
    }
}
