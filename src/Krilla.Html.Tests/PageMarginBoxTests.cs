/// <summary>
/// <c>@page</c> margin boxes: the running headers, footers and page numbers a document declares.
///
/// The corpus cannot measure any of this. CHROMIUM IMPLEMENTS NONE OF IT — a reference generated
/// from a document with an <c>@top-center</c> comes back with an empty margin — so a scenario would
/// record the browser's absence as the target and fail the moment the feature worked. These are
/// measured against the specification and against the ink instead.
///
/// Ink, specifically: a margin box generates no element, so there is nothing for the geometry
/// comparison to name and nothing but the page to look at. Every case colours its content
/// <c>#00c000</c> and asks where on the page that colour landed.
/// </summary>
public class PageMarginBoxTests
{
    /// <summary>
    /// A margin box appears on every page, in the strip its name gives.
    /// </summary>
    [Test]
    public async Task ATopBoxIsDrawnInTheTopMarginOfEveryPage()
    {
        var html = Document(
            """@page { @top-center { content: "Quarterly report"; color: #00c000 } }""",
            pages: 2);

        await Assert.That(await PageCount(html)).IsEqualTo(2);

        foreach (var page in new[] {0, 1})
        {
            var ink = await Marked(html, page);

            await Assert.That(ink).IsNotNull();
            await Assert.That(ink!.Value.Bottom).IsLessThanOrEqualTo(Margin);
        }
    }

    /// <summary>
    /// A bottom box lands in the bottom margin, which is the other end of the same rule.
    /// </summary>
    /// <remarks>
    /// Worth its own case: a top box at the page's origin is where an unpositioned box would land
    /// anyway, so it alone cannot say the strip was chosen rather than defaulted to.
    /// </remarks>
    [Test]
    public async Task ABottomBoxIsDrawnInTheBottomMargin()
    {
        var html = Document(
            """@page { @bottom-center { content: "Confidential"; color: #00c000 } }""",
            pages: 1);

        var ink = await Marked(html, 0);

        await Assert.That(ink).IsNotNull();
        await Assert.That(ink!.Value.Top).IsGreaterThanOrEqualTo(CorpusLayout.PageHeight - Margin);
    }

    /// <summary>
    /// <c>counter(page)</c> and <c>counter(pages)</c> are the two counters that only exist here.
    /// </summary>
    /// <remarks>
    /// The whole reason an author reaches for a margin box, and the reason the boxes are built per
    /// page rather than once: the content is a different string on every sheet.
    /// </remarks>
    [Test]
    public async Task ThePageCountersAreResolvedPerPage()
    {
        var html = Document(
            """@page { @bottom-right { content: counter(page) " of " counter(pages); color: #00c000 } }""",
            pages: 3);

        await Assert.That(await Text(html, 0)).IsEqualTo("1 of 3");
        await Assert.That(await Text(html, 1)).IsEqualTo("2 of 3");
        await Assert.That(await Text(html, 2)).IsEqualTo("3 of 3");
    }

    /// <summary>
    /// A counter style applies to the page number, as it does to a list marker.
    /// </summary>
    [Test]
    public async Task APageCounterTakesACounterStyle()
    {
        var html = Document(
            """@page { @bottom-center { content: counter(page, upper-roman); color: #00c000 } }""",
            pages: 3);

        await Assert.That(await Text(html, 2)).IsEqualTo("III");
    }

    /// <summary>
    /// <c>:first</c> selects the title page, which is how a header is kept off it.
    /// </summary>
    /// <remarks>
    /// The single most common thing anyone writes a page selector for, and the case the
    /// specificity order exists to serve: <c>:first</c> outranks the bare rule, so the later,
    /// less specific one does not win it back.
    /// </remarks>
    [Test]
    public async Task FirstSelectsThePageItNames()
    {
        var html = Document(
            """
            @page { @top-center { content: "Quarterly report"; color: #00c000 } }
            @page :first { @top-center { content: none } }
            """,
            pages: 2);

        await Assert.That(await Marked(html, 0)).IsNull();
        await Assert.That(await Marked(html, 1)).IsNotNull();
    }

    /// <summary>
    /// <c>:left</c> and <c>:right</c> select by sheet, counting page one as a right-hand one.
    /// </summary>
    [Test]
    public async Task LeftAndRightSelectBySheet()
    {
        var html = Document(
            """
            @page :right { @top-right { content: "recto"; color: #00c000 } }
            @page :left { @top-left { content: "verso"; color: #00c000 } }
            """,
            pages: 2);

        await Assert.That(await Text(html, 0)).IsEqualTo("recto");
        await Assert.That(await Text(html, 1)).IsEqualTo("verso");
    }

    /// <summary>
    /// The slot's name decides where along its strip the content sits.
    /// </summary>
    /// <remarks>
    /// What makes "title on the left, page number on the right" two rules and no alignment
    /// declarations — and the one thing about the approximation in <see cref="PageMarginSlots"/>
    /// that is observable: each box is given the whole strip and placed by its own alignment,
    /// rather than the strip being divided into three.
    /// </remarks>
    [Test]
    public async Task TheSlotNameDecidesTheAlignment()
    {
        var middle = CorpusLayout.PageWidth / 2;

        var left = await Marked(
            Document("""@page { @top-left { content: "L"; color: #00c000 } }""", pages: 1),
            0);

        var right = await Marked(
            Document("""@page { @top-right { content: "R"; color: #00c000 } }""", pages: 1),
            0);

        await Assert.That(left!.Value.Right).IsLessThan(middle);
        await Assert.That(right!.Value.Left).IsGreaterThan(middle);
    }

    /// <summary>
    /// A margin box is styled by its own declarations and inherits nothing from the document.
    /// </summary>
    /// <remarks>
    /// CSS says its parent is the page context rather than the body, so a document that colours
    /// its text still gets the footer the <c>@page</c> rule asked for. Measured the other way
    /// round, since the marker colour is what everything here looks for: <c>body</c> is coloured
    /// and the box is not, so a box that inherited would be invisible to the search.
    /// </remarks>
    [Test]
    public async Task AMarginBoxDoesNotInheritFromTheDocument()
    {
        var html = Document(
            """
            body { color: #c00000 }
            @page { @top-center { content: "header"; color: #00c000 } }
            """,
            pages: 1);

        await Assert.That(await Marked(html, 0)).IsNotNull();
    }

    /// <summary>
    /// A margin with no room produces no box.
    /// </summary>
    /// <remarks>
    /// The strip is the page margin, so a document that sets none has nowhere to put a header —
    /// and a box drawn anyway would sit over the first line of the text.
    /// </remarks>
    [Test]
    public async Task AZeroMarginLeavesNoRoom()
    {
        var html = Document(
            """@page { margin: 0; @top-center { content: "header"; color: #00c000 } }""",
            pages: 1,
            margin: 0);

        await Assert.That(await Marked(html, 0)).IsNull();
    }

    /// <summary>
    /// A margin box with no <c>content</c> generates nothing, borders and all.
    /// </summary>
    /// <remarks>
    /// CSS's own rule, and the mechanism behind <c>content: none</c> on a selector: the box does
    /// not exist rather than existing empty, so its border and background go with it.
    /// </remarks>
    [Test]
    public async Task AMarginBoxWithNoContentGeneratesNothing()
    {
        var html = Document(
            """@page { @top-center { border-bottom: 4px solid #00c000 } }""",
            pages: 1);

        await Assert.That(await Marked(html, 0)).IsNull();
    }

    /// <summary>
    /// A margin box takes the page's own <c>@page</c> margins, not the caller's.
    /// </summary>
    /// <remarks>
    /// The two are settled in that order for the geometry already; this is the check that the
    /// strips are measured from the result rather than from what the caller passed.
    /// </remarks>
    [Test]
    public async Task TheStripFollowsTheDeclaredMargin()
    {
        var html = Document(
            """
            @page { margin: 200px; @top-center { content: "header"; color: #00c000 } }
            """,
            pages: 1);

        var ink = await Marked(html, 0);

        await Assert.That(ink).IsNotNull();
        await Assert.That(ink!.Value.Bottom).IsLessThanOrEqualTo(200);
        await Assert.That(ink.Value.Top).IsGreaterThan(Margin);
    }

    /// <summary>
    /// A name that is not one of the sixteen is reported rather than silently dropped.
    /// </summary>
    /// <remarks>
    /// The realistic case is a spelling mistake, and a running header that does not appear is
    /// exactly the kind of absence nobody notices until the document is printed.
    /// </remarks>
    [Test]
    public async Task AnUnknownMarginBoxNameIsReported()
    {
        var reports = new List<HtmlDiagnostic>();

        var options = Options(Margin);
        options.OnDiagnostic = reports.Add;

        await HtmlConverter.ConvertAsync(
            Document("""@page { @top-centre-ish { content: "header" } }""", pages: 1),
            options);

        await Assert.That(reports.Select(_ => _.Value)).Contains("@top-centre-ish");
    }

    /// <summary>
    /// A named page is reported and applies to nothing.
    /// </summary>
    /// <remarks>
    /// It selects the elements carrying <c>page: cover</c>, which this engine does not read.
    /// Applying it to every page instead would put a cover sheet's header on all of them, which is
    /// worse than the header being absent and much harder to attribute.
    /// </remarks>
    [Test]
    public async Task ANamedPageIsReportedAndSelectsNothing()
    {
        var html = Document(
            """@page cover { @top-center { content: "header"; color: #00c000 } }""",
            pages: 1);

        var reports = new List<HtmlDiagnostic>();

        var options = Options(Margin);
        options.OnDiagnostic = reports.Add;

        await HtmlConverter.ConvertAsync(html, options);

        await Assert.That(reports.Select(_ => _.Value)).Contains("cover");
        await Assert.That(await Marked(html, 0)).IsNull();
    }

    /// <summary>
    /// <c>string-set</c> and <c>string()</c>: a running header that names the section it is on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CSS's own running-header mechanism, and the reason most documents have an <c>@page</c> rule
    /// at all. Both halves are recovered from the stylesheet's own SOURCE, because AngleSharp drops
    /// the declarations: <c>string-set: title content()</c> comes back empty and indistinguishable
    /// from one nobody wrote, and so does <c>content: string(title)</c> outside the <c>@page</c>
    /// scan.
    /// </para>
    /// <para>
    /// The rule is <c>first</c>, which is the property's default: the value assigned by the first
    /// element on the page that sets it, and otherwise whatever was carried forward. Page two is
    /// what says so — it holds no heading of its own and keeps page one's.
    /// </para>
    /// </remarks>
    [Test]
    public async Task ANamedStringFollowsTheSectionItIsOn()
    {
        var html = Sections("""
                            h2 { string-set: title content() }
                            @page { @top-center { content: "[" string(title) "]" } }
                            """);

        await Assert.That(await Text(html, 0)).Contains("[Materials]");
        await Assert.That(await Text(html, 1)).Contains("[Materials]");
        await Assert.That(await Text(html, 2)).Contains("[Method]");
    }

    /// <summary>
    /// A named string a document never sets reads as nothing, and takes the box with it.
    /// </summary>
    /// <remarks>
    /// <c>content</c> decides whether a margin box EXISTS, so a header whose only content is an
    /// unset string is not drawn at all rather than drawn empty — which matters, because an empty
    /// box would still paint its own border and background.
    /// </remarks>
    [Test]
    public async Task AnUnsetNamedStringDrawsNothing()
    {
        var html = Sections("""@page { @top-center { content: string(missing) } }""");

        await Assert.That(await Text(html, 0)).IsEqualTo("Materials");
    }

    /// <summary>
    /// <c>string-set</c> takes the whole <c>content</c> grammar, not just <c>content()</c>.
    /// </summary>
    /// <remarks>
    /// Several values concatenate, which is what lets a running header carry the section's number
    /// as well as its name — and the counter is read where the ELEMENT is, not where the page is,
    /// so it holds the value that heading had rather than the one in force at the end.
    /// </remarks>
    [Test]
    public async Task ANamedStringConcatenatesItsValues()
    {
        var html = Sections("""
                            body { counter-reset: part }
                            h2 { counter-increment: part; string-set: title "Part " counter(part) ": " content() }
                            @page { @top-center { content: string(title) } }
                            """);

        await Assert.That(await Text(html, 0)).Contains("Part 1: Materials");
        await Assert.That(await Text(html, 2)).Contains("Part 2: Method");
    }

    /// <summary>
    /// Three pages: a heading, a page of filler, and a second heading.
    /// </summary>
    /// <remarks>
    /// The middle page is the one that matters. It carries no heading of its own, so what its
    /// running header says is entirely a question of what was carried forward — and an
    /// implementation that read the LAST assignment in the document, or the nearest one in either
    /// direction, gets it wrong while getting both other pages right.
    /// </remarks>
    static string Sections(string css) =>
        $$"""
          <!doctype html>
          <html><head><style>
            html, body, div, h2 { margin: 0; padding: 0 }
            body { font-family: "Liberation Sans"; font-size: 16px; line-height: 24px }
            h2 { font-size: 16px; line-height: 24px }
            .filler { height: {{CorpusLayout.PageHeight - 2 * Margin - 24}}px }
            .whole { height: {{CorpusLayout.PageHeight - 2 * Margin}}px }
            {{css}}
          </style></head>
          <body>
            <h2>Materials</h2><div class="filler"></div>
            <div class="whole"></div>
            <h2>Method</h2><div class="filler"></div>
          </body></html>
          """;

    /// <summary>
    /// A <c>url()</c> in a margin box resolves through the document's own image store.
    /// </summary>
    /// <remarks>
    /// A logo in a running header is the case, and the store is the point: a margin box is part of
    /// the document's stylesheet, so an image it names is bound by the same policy as one an
    /// <c>&lt;img&gt;</c> names. A `data:` URI here rather than a file, its bytes being in the
    /// document already.
    /// </remarks>
    [Test]
    public async Task AMarginBoxDrawsAnImage()
    {
        // One 8x8 green PNG.
        const string pixel =
            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAE0lEQVR4nGNkOMCAFTBhFx6sEgCbiADQJb2+YAAAAABJRU5ErkJggg==";

        var html = Document(
            $$"""@page { @top-center { content: url("{{pixel}}") } }""",
            pages: 1);

        var ink = await Marked(html, 0);

        await Assert.That(ink).IsNotNull();
        await Assert.That(ink!.Value.Bottom).IsLessThanOrEqualTo(Margin);
    }

    const float Margin = 72;

    /// <summary>The extent of the marker colour on a page, or null when it is not there.</summary>
    static async Task<(float Left, float Top, float Right, float Bottom)?> Marked(string html, int page)
    {
        var image = await Render(html, page);

        float left = image.Width, top = image.Height, right = -1, bottom = -1;

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var offset = (y * image.Width + x) * 4;
                var rgba = image.Rgba;

                // Green has to DOMINATE, not merely be present. A test on each channel's
                // absolute value matches mid-grey — which is every antialiased edge of every
                // black glyph on the page, and made the document's own text read as the marker.
                if (rgba[offset + 1] - rgba[offset] > 48 &&
                    rgba[offset + 1] - rgba[offset + 2] > 48)
                {
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }
        }

        if (right < 0)
        {
            return null;
        }

        return (left, top, right, bottom);
    }

    /// <summary>The text of the one margin box on a page, read out of the PDF.</summary>
    /// <remarks>
    /// Pixels say where; only the text says what, and <c>counter(page)</c> is entirely about what.
    /// </remarks>
    static async Task<string> Text(string html, int page)
    {
        using var document = PdfiumDocument.Load(await HtmlConverter.ConvertAsync(html, Options(Margin)));
        using var sheet = document.LoadPage(page);

        return (sheet.GetText() ?? "").Trim();
    }

    static string Document(string css, int pages, float margin = Margin)
    {
        var body = new StringBuilder();

        for (var block = 0; block < pages; block++)
        {
            // Deliberately empty. The page's text is read back to check `counter(page)`, and a
            // document with words in it would put them in the same string.
            body.Append("""<div class="filler"></div>""");
        }

        return $$"""
                 <!doctype html>
                 <html><head><style>
                   html, body, div { margin: 0; padding: 0 }
                   body { font-family: "Liberation Sans"; font-size: 16px; line-height: 24px }
                   .filler { height: {{CorpusLayout.PageHeight - 2 * margin}}px }
                   {{css}}
                 </style></head>
                 <body>{{body}}</body></html>
                 """;
    }

    static HtmlOptions Options(float margin) =>
        new HtmlOptions
            {
                PageWidth = CorpusLayout.PageWidth,
                PageHeight = CorpusLayout.PageHeight,
                Fonts = CorpusRunner.Options().Fonts
            }
            .WithMargin(margin);

    static async Task<int> PageCount(string html)
    {
        using var document = PdfiumDocument.Load(await HtmlConverter.ConvertAsync(html, Options(Margin)));
        return document.PageCount;
    }

    static async Task<PngImage> Render(string html, int index)
    {
        using var document = PdfiumDocument.Load(await HtmlConverter.ConvertAsync(html, Options(Margin)));
        var png = document.RenderPage(index, new RenderOptions
        {
            Dpi = CorpusLayout.Dpi
        });

        using var stream = new MemoryStream(png);
        return PngDecoder.Decode(stream);
    }
}
