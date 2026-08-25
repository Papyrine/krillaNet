/// <summary>
/// <c>orphans</c> and <c>widows</c>, in the arrangements the corpus does not hold.
///
/// It holds two. <c>page/orphans_widows</c> measures the rule's two halves against Chromium — a
/// run moved whole because too few lines fit above the break, and a break moved earlier because
/// too few fall below it — and <c>page/break_between_lines</c> measures the case where neither
/// count can be met, where the browser splits rather than moving. What is here is everything
/// around those: the counts already satisfied, a run of one line, a run taller than the page, and
/// the switch itself.
///
/// The note that used to stand at the top of this file said the browser implemented neither
/// property. It does, and nothing in the corpus could tell, because every scenario that broke
/// inside a paragraph happened to break where both counts permit.
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
    public async Task TurningTheConstraintOffChangesNothingHere()
    {
        // The control. Two lines above the break and one below, which the initial counts forbid —
        // and this run is too short to fix, so the constrained answer is the unconstrained one.
        // Both leave ink low on the first page, where a run moved whole would leave none.
        await Assert.That(LastInkedRow(await Render(Straddling, Options(constrain: false), 0)))
            .IsEqualTo(LastInkedRow(await Render(Straddling, Options(), 0)));

        await Assert.That(LastInkedRow(await Render(Straddling, Options(), 0))).IsGreaterThan(1010);
    }

    /// <summary>
    /// A run that cannot satisfy both counts keeps its break rather than moving whole.
    /// </summary>
    /// <remarks>
    /// MEASURED, and the opposite of what this did first. Moving the whole run overleaf is the
    /// tidier-looking answer and is what a print engine is often described as doing; Chromium
    /// splits, and <c>page/break_between_lines</c> holds the reference — two lines above the break
    /// and one below, under the initial <c>widows: 2</c> that forbids exactly that.
    /// </remarks>
    /// <remarks>
    /// Three lines, and <c>widows: 2</c> wants two below a break that can leave at most one
    /// without stranding a single line above. Nothing satisfies both, so the break stays and the
    /// last line on the page sits low.
    /// </remarks>
    [Test]
    public async Task NeitherConstraintSatisfiableKeepsTheBreak() =>
        await Assert.That(LastInkedRow(await Render(Straddling, Options(), 0))).IsGreaterThan(1010);

    [Test]
    public async Task TooFewLinesAboveMovesTheWholeRun()
    {
        // Three of each on a three-line run: two above the break is one short of `orphans`, and
        // moving the break earlier only takes more lines off the top. Nothing but moving the whole
        // paragraph fixes it, and that is what Chromium does — `page/orphans_widows`' first half
        // measures the same rule at the initial counts.
        var html = Straddling.Replace(
            "line-height: 32px;",
            "line-height: 32px; orphans: 3; widows: 3;");

        await Assert.That(LastInkedRow(await Render(html, Options(), 0))).IsEqualTo(-1);
    }

    /// <summary>
    /// The properties are read from the cascade rather than assumed, and the switch turns them off.
    /// </summary>
    /// <remarks>
    /// The one thing the corpus cannot state, its scenarios all running with the default. A caller
    /// who wants a break decided by height alone — a form, a ticket, anything whose lines are not
    /// prose — gets one.
    /// </remarks>
    [Test]
    public async Task TheSwitchTurnsTheConstraintOff()
    {
        var html = Straddling.Replace(
            "line-height: 32px;",
            "line-height: 32px; orphans: 3; widows: 3;");

        await Assert.That(LastInkedRow(await Render(html, Options(), 0))).IsEqualTo(-1);
        await Assert.That(LastInkedRow(await Render(html, Options(constrain: false), 0)))
            .IsGreaterThan(1010);
    }

    [Test]
    public async Task TheRunStillPaginatesRatherThanLooping()
    {
        // The guard that matters most: moving a run earlier must always advance the page top, or
        // the loop that produces page tops never terminates. A paragraph taller than the page it
        // would move to is the case that tests it.
        var options = Options();

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

        using var document = PdfiumDocument.Load(await HtmlConverter.ConvertAsync(tall, options));

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

        var first = await Render(html, options, 0);

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

        using var document = PdfiumDocument.Load(await HtmlConverter.ConvertAsync(html, options));

        // The line does not fit under the 1040px block, so it moves to the second page as one
        // unbreakable unit — two pages, not three, and no attempt to constrain a run of one.
        await Assert.That(document.PageCount).IsEqualTo(2);
    }

    static HtmlOptions Options(bool constrain = true) =>
        new()
        {
            PageWidth = CorpusLayout.PageWidth,
            PageHeight = CorpusLayout.PageHeight,
            Fonts = CorpusRunner.Options().Fonts,
            HonourOrphansAndWidows = constrain
        };

    static async Task<PngImage> Render(string html, HtmlOptions options, int index)
    {
        using var document = PdfiumDocument.Load(await HtmlConverter.ConvertAsync(html, options));
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
