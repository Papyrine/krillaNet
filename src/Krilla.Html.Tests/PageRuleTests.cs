/// <summary>
/// <c>@page</c> rules and print media queries, neither of which the corpus can measure.
///
/// The corpus fixes its page at 816 by 1056 in <see cref="CorpusLayout"/>, because the browser
/// reference and the comparison have to rasterise to the same dimensions — so a scenario that
/// changed the page size would produce an image the reference could not be compared against, and
/// SSIM would be suppressed entirely rather than reported as a difference. Page geometry is
/// therefore asserted here, from the rendered page's own dimensions.
///
/// Print media is here for a related reason: the corpus reference resolves print media on both
/// halves now, so a scenario COULD measure it — but what wants pinning is the direction, that
/// `@media print` applies and `@media screen` does not, and one assertion says that where a
/// scenario would need two.
/// </summary>
public class PageRuleTests
{
    const string Body = "<div id=\"a\">content</div>";

    [Test]
    public async Task ANamedSizeReplacesThePage()
    {
        var page = await Render("@page { size: A4 }");

        // A4 is 210 by 297 millimetres, which at 96 pixels per inch is 793.7 by 1122.5 — so the
        // rendered page is neither the caller's Letter nor a round number.
        await Assert.That(page.Width).IsEqualTo(794);
        await Assert.That(page.Height).IsEqualTo(1123);
    }

    [Test]
    public async Task AnOrientationAloneTurnsTheCallersPaper()
    {
        var page = await Render("@page { size: landscape }");

        // No size named, so the paper stays US Letter and only the axes swap.
        await Assert.That(page.Width).IsEqualTo(CorpusLayout.PageHeight);
        await Assert.That(page.Height).IsEqualTo(CorpusLayout.PageWidth);
    }

    [Test]
    public async Task AnOrientationAppliesToTheSizeBesideIt()
    {
        var page = await Render("@page { size: A4 landscape }");

        await Assert.That(page.Width).IsEqualTo(1123);
        await Assert.That(page.Height).IsEqualTo(794);
    }

    [Test]
    public async Task PortraitLeavesAPortraitPageAlone()
    {
        // The keyword names the orientation the page must END in, not a turn to apply — so asking
        // a portrait page to be portrait has to be a no-op rather than a transpose.
        var page = await Render("@page { size: portrait }");

        await Assert.That(page.Width).IsEqualTo(CorpusLayout.PageWidth);
        await Assert.That(page.Height).IsEqualTo(CorpusLayout.PageHeight);
    }

    [Test]
    public async Task TwoLengthsAreAWidthAndAHeight()
    {
        var page = await Render("@page { size: 20cm 10cm }");

        await Assert.That(page.Width).IsEqualTo(756);
        await Assert.That(page.Height).IsEqualTo(378);
    }

    [Test]
    public async Task OneLengthIsASquarePage()
    {
        // The specification's rule, and not what an author writing a width expects. Worth pinning
        // precisely because it is surprising: a reading that treated the single length as a width
        // and kept the caller's height would look reasonable and be wrong.
        var page = await Render("@page { size: 10cm }");

        await Assert.That(page.Width).IsEqualTo(378);
        await Assert.That(page.Height).IsEqualTo(378);
    }

    [Test]
    public async Task LengthsAreNotTurnedByAnOrientationKeyword()
    {
        // Explicit dimensions are already in the order the author wanted, so `landscape` beside
        // them must not transpose a page they already transposed.
        var page = await Render("@page { size: 20cm 10cm landscape }");

        await Assert.That(page.Width).IsEqualTo(756);
        await Assert.That(page.Height).IsEqualTo(378);
    }

    [Test]
    public async Task PageMarginsInsetTheContent()
    {
        // An inch rather than a centimetre: it converts to exactly 96 pixels, so the content edge
        // lands on a pixel boundary and the assertion is about the margin rather than about how a
        // rasteriser splits a partly covered row. `TwoLengthsAreAWidthAndAHeight` covers the
        // centimetre conversion, where a fractional result is the whole point.
        var page = await Render("@page { margin: 1in }", "<div id=\"a\" style=\"height: 40px; background: #c81e1e\"></div>");

        await Assert.That(FirstInkedRow(page)).IsEqualTo(96);
        await Assert.That(FirstInkedColumn(page)).IsEqualTo(96);
        await Assert.That(LastInkedColumn(page)).IsEqualTo(CorpusLayout.PageWidth - 96 - 1);
    }

    [Test]
    public async Task PageMarginsAreReadPerEdge()
    {
        var page = await Render(
            "@page { margin: 30px 0 0 50px }",
            "<div id=\"a\" style=\"height: 40px; background: #c81e1e\"></div>");

        await Assert.That(FirstInkedRow(page)).IsEqualTo(30);
        await Assert.That(FirstInkedColumn(page)).IsEqualTo(50);
    }

    [Test]
    public async Task ALaterRuleWins()
    {
        // `@page` has no specificity to compare — every rule selects the same page box — so the
        // last declaration of a property is the one that applies.
        var page = await Render("@page { size: A4 } @page { size: 10cm 10cm }");

        await Assert.That(page.Width).IsEqualTo(378);
        await Assert.That(page.Height).IsEqualTo(378);
    }

    [Test]
    public async Task RulesAreIgnoredWhenTheCallerSaysSo()
    {
        var options = Options();
        options.HonourPageRules = false;

        var page = await Render("@page { size: A4 }", Body, options);

        await Assert.That(page.Width).IsEqualTo(CorpusLayout.PageWidth);
        await Assert.That(page.Height).IsEqualTo(CorpusLayout.PageHeight);
    }

    [Test]
    public async Task ADocumentWithNoPageRuleIsUnchanged()
    {
        // The default is to honour them, so the case that matters most is the one where there is
        // nothing to honour: reading no rule must leave every edge exactly as the caller set it.
        var options = Options();
        options.MarginTop = 17;

        var page = await Render("", "<div id=\"a\" style=\"height: 40px; background: #c81e1e\"></div>", options);

        await Assert.That(page.Width).IsEqualTo(CorpusLayout.PageWidth);
        await Assert.That(FirstInkedRow(page)).IsEqualTo(17);
    }

    [Test]
    public async Task ThePageRuleDoesNotMutateTheCallersOptions()
    {
        // The same options object is often reused across conversions, and one document's page size
        // leaking into the next would be a memorable bug.
        var options = Options();

        await Render("@page { size: A4 }", Body, options);

        await Assert.That(options.PageWidth).IsEqualTo(CorpusLayout.PageWidth);
        await Assert.That(options.PageHeight).IsEqualTo(CorpusLayout.PageHeight);
    }

    [Test]
    public async Task PrintMediaApplies()
    {
        // A PDF is print. Resolving media against the screen category meant the block written FOR
        // this conversion was excluded while the one written for a screen was applied.
        // The plain rule first and the media block after, so the media block wins on order when
        // it applies at all — otherwise the test would pass on a document where neither did.
        var page = await Render(
            "div { height: 0 } @media print { div { height: 40px; background: #c81e1e } }",
            "<div id=\"a\"></div>");

        await Assert.That(LastInkedRow(page)).IsEqualTo(39);
    }

    [Test]
    public async Task ScreenMediaDoesNot()
    {
        var page = await Render(
            "div { height: 0 } @media screen { div { height: 40px; background: #c81e1e } }",
            "<div id=\"a\"></div>");

        await Assert.That(LastInkedRow(page)).IsEqualTo(-1);
    }

    [Test]
    public async Task ViewportUnitsFollowThePageRule()
    {
        // The page geometry has to be settled BEFORE the cascade is read, or a document declaring
        // its own paper is laid out against the wrong rectangle. A viewport unit is the cheapest
        // way to see that: `50vw` of an A4 page is not `50vw` of a Letter one.
        //
        // Asserted on the measured BOX rather than on where the ink stops. Half of 793.7008 is
        // 396.8504, which is not a whole number of pixels — and a fractional edge is rasterised
        // through a round trip into PDF points, so the last inked column answers a question about
        // the rasteriser rather than about the page rule.
        var html =
            "<!doctype html><html><head><style>html, body { margin: 0; padding: 0; }" +
            "@page { size: A4 } div { width: 50vw; height: 40px }" +
            "</style></head><body><div id=\"a\"></div></body></html>";

        var options = Options();
        using var document = await HtmlConverter.ParseAsync(html, options);

        var boxes = await BoxDump.MeasureAsync(html, HtmlConverter.Paged(document, options));
        var div = boxes.Single(_ => _.Selector.EndsWith("div:nth-child(1)"));

        await Assert.That(div.Width).IsEqualTo(396.85f).Within(0.01f);

        // And not half of Letter, which is what it would be if the rule were read too late.
        await Assert.That(div.Width).IsNotEqualTo(CorpusLayout.PageWidth / 2f);
    }

    static Task<PngImage> Render(string css) =>
        Render(css, Body);

    static Task<PngImage> Render(string css, string body) =>
        Render(css, body, Options());

    static async Task<PngImage> Render(string css, string body, HtmlOptions options)
    {
        var html =
            "<!doctype html><html><head><style>html, body { margin: 0; padding: 0; }" +
            css +
            "</style></head><body>" +
            body +
            "</body></html>";

        using var document = PdfiumDocument.Load(await HtmlConverter.ConvertAsync(html, options));
        var png = document.RenderPage(0, new RenderOptions {Dpi = CorpusLayout.Dpi});

        using var stream = new MemoryStream(png);
        return PngDecoder.Decode(stream);
    }

    static HtmlOptions Options() =>
        new()
        {
            PageWidth = CorpusLayout.PageWidth,
            PageHeight = CorpusLayout.PageHeight,
            Fonts = CorpusRunner.Options().Fonts
        };

    static int FirstInkedRow(PngImage image)
    {
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (Inked(image, x, y))
                {
                    return y;
                }
            }
        }

        return -1;
    }

    static int LastInkedRow(PngImage image)
    {
        for (var y = image.Height - 1; y >= 0; y--)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (Inked(image, x, y))
                {
                    return y;
                }
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
                if (Inked(image, x, y))
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
            for (var y = 0; y < image.Height; y++)
            {
                if (Inked(image, x, y))
                {
                    return x;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// Whether a pixel carries ink, which here means anything that is not near-white.
    /// </summary>
    /// <remarks>
    /// A loose threshold on purpose, matching <see cref="PageMarginTests"/>: the edge of a filled
    /// rectangle is antialiased, and demanding an exact colour would make the answer depend on
    /// where that edge landed.
    /// </remarks>
    static bool Inked(PngImage image, int x, int y)
    {
        var offset = (y * image.Width + x) * 4;
        var rgba = image.Rgba;
        return rgba[offset] < 240 || rgba[offset + 1] < 240 || rgba[offset + 2] < 240;
    }
}
