/// <summary>
/// Content drawn on more than one page: a repeating table header, and a fixed box.
///
/// The two corpus scenarios — <c>page/table_header</c> and <c>page/fixed_repeat</c> — measure what
/// a browser does with each in the shape an author writes. What is here is the edges of that: the
/// cases where nothing should repeat, which a browser reference cannot express, since a scenario
/// whose expected output is "the same page again, without the extra thing" measures nothing a
/// reference also lacking it would catch.
///
/// Every case is a colour on a page. A repeated box is drawn rather than laid out, so the box tree
/// says the same thing whether it was repeated or not, and the pixels are the only place the
/// answer exists.
/// </summary>
public class RunningContentTests
{
    /// <summary>The fill every case looks for. Nothing else in these documents is green.</summary>
    static readonly byte[] marker = [0x00, 0xC0, 0x00];

    /// <summary>
    /// A header group is re-drawn at the top of the page its table continues onto.
    /// </summary>
    /// <remarks>
    /// The band it fills is reserved, so the first continued row starts below it rather than under
    /// it — which is what separates a repeated header from one merely painted over the content.
    /// </remarks>
    [Test]
    public async Task AHeaderGroupRepeatsOnAContinuationPage()
    {
        var html = Ledger(rows: 30, css: "");

        await Assert.That(await PageCount(html)).IsEqualTo(2);

        var second = await Render(html, 1);

        await Assert.That(MarkedRows(second)).IsNotEmpty();
        await Assert.That(MarkedRows(second)[0]).IsLessThan(8);
    }

    /// <summary>
    /// A table with no header group reserves nothing, and the continuation page starts flush.
    /// </summary>
    /// <remarks>
    /// The control for the case above. Without it a band reserved unconditionally would pass every
    /// other assertion here.
    /// </remarks>
    [Test]
    public async Task ATableWithNoHeaderGroupReservesNothing()
    {
        var html = Ledger(rows: 30, css: "", header: false);

        await Assert.That(await PageCount(html)).IsEqualTo(2);
        await Assert.That(MarkedRows(await Render(html, 1))).IsEmpty();
        await Assert.That(FirstInkedRow(await Render(html, 1))).IsEqualTo(0);
    }

    /// <summary>
    /// The header stops repeating once the table has ended.
    /// </summary>
    /// <remarks>
    /// A page holding the paragraph AFTER a table should not carry that table's column headings,
    /// which makes the extent of the table the rule rather than the page number. The trailing box
    /// is tall enough to want a page of its own, so page three begins past the table's bottom
    /// edge.
    /// </remarks>
    [Test]
    public async Task TheHeaderStopsRepeatingPastTheTable()
    {
        var html = Ledger(
            rows: 30,
            css: "#after { height: 1000px; background: #d0d0d0 }",
            after: """<div id="after"></div>""");

        await Assert.That(await PageCount(html)).IsEqualTo(3);

        // Page two still holds table rows, so the header is on it; page three is all `#after`.
        await Assert.That(MarkedRows(await Render(html, 1))).IsNotEmpty();
        await Assert.That(MarkedRows(await Render(html, 2))).IsEmpty();
    }

    /// <summary>
    /// A header taller than half the page is not repeated at all.
    /// </summary>
    /// <remarks>
    /// Reserving it would turn every continuation page into mostly header, and a header taller
    /// than the page itself would leave the slice nothing to advance through — so the page count
    /// would follow the length of the document rather than its height. A browser stops repeating
    /// for the same reason.
    /// </remarks>
    [Test]
    public async Task AHeaderTallerThanHalfThePageIsNotRepeated()
    {
        var html = Ledger(rows: 30, css: "#ledger th { height: 700px }");

        await Assert.That(MarkedRows(await Render(html, 1))).IsEmpty();
    }

    /// <summary>
    /// An anchored fixed box is drawn at the same place on every page.
    /// </summary>
    /// <remarks>
    /// The corpus measures this against Chromium over three sheets. Here for the property the
    /// corpus cannot state — that it is the SAME place, rather than merely present — and because
    /// the case below is only meaningful beside it.
    /// </remarks>
    [Test]
    public async Task AnAnchoredFixedBoxIsDrawnOnEveryPage()
    {
        var html = Filler(
            """<div id="banner"></div>""",
            "#banner { position: fixed; top: 120px; left: 0; width: 200px; height: 40px; background: #00c000 }");

        await Assert.That(await PageCount(html)).IsEqualTo(3);

        for (var page = 0; page < 3; page++)
        {
            var rows = MarkedRows(await Render(html, page));

            await Assert.That(rows).IsNotEmpty();
            await Assert.That(rows[0]).IsEqualTo(120);
        }
    }

    /// <summary>
    /// A fixed box with neither <c>top</c> nor <c>bottom</c> is drawn once, where flow put it.
    /// </summary>
    /// <remarks>
    /// Its position is its STATIC position, which is a position in the document rather than on a
    /// page — so repeating it would add each page's own top to a coordinate that already includes
    /// it, and a box whose flow position is on a later page would fall off the bottom of every
    /// page and vanish from a document it currently appears in. Reported through
    /// <c>OnDiagnostic</c> rather than silently diverging.
    /// </remarks>
    [Test]
    public async Task AnUnanchoredFixedBoxIsDrawnOnce()
    {
        var html = Filler(
            """<div id="banner"></div>""",
            "#banner { position: fixed; width: 200px; height: 40px; background: #00c000 }");

        await Assert.That(await PageCount(html)).IsEqualTo(3);
        await Assert.That(MarkedRows(await Render(html, 0))).IsNotEmpty();
        await Assert.That(MarkedRows(await Render(html, 1))).IsEmpty();
        await Assert.That(MarkedRows(await Render(html, 2))).IsEmpty();
    }

    /// <summary>
    /// A fixed box takes no part in deciding where pages end.
    /// </summary>
    /// <remarks>
    /// It is on the page it is drawn on by definition, so there is nothing for a break to fall
    /// inside and nothing it can lengthen. Counted in, a footer anchored near the foot of the page
    /// would read as content the document has to make room for and add a page holding nothing
    /// else.
    /// </remarks>
    [Test]
    public async Task AFixedBoxAddsNoPage()
    {
        var html = Filler(
            """<div id="banner"></div>""",
            "#banner { position: fixed; bottom: 0; right: 0; width: 200px; height: 40px; background: #00c000 }",
            fillers: 1);

        await Assert.That(await PageCount(html)).IsEqualTo(1);
    }

    /// <summary>
    /// A table whose header repeats, with <paramref name="rows"/> body rows.
    /// </summary>
    static string Ledger(int rows, string css, bool header = true, string after = "")
    {
        var body = new StringBuilder("<table id=\"ledger\">");

        if (header)
        {
            body.Append("<thead><tr><th>Reference</th><th>Amount</th></tr></thead>");
        }

        body.Append("<tbody>");

        for (var row = 0; row < rows; row++)
        {
            body.Append($"<tr><td>A-{row:000}</td><td>{row * 7}.50</td></tr>");
        }

        body.Append("</tbody></table>");
        body.Append(after);

        return Document(
            body.ToString(),
            $$"""
              #ledger { border-collapse: collapse; width: 400px }
              #ledger th, #ledger td { border: 1px solid #909090; padding: 8px }
              #ledger th { background: #00c000 }
              {{css}}
              """);
    }

    /// <summary>Three pages of plain blocks, plus whatever the case adds.</summary>
    static string Filler(string body, string css, int fillers = 3)
    {
        var blocks = new StringBuilder(body);

        for (var block = 0; block < fillers; block++)
        {
            blocks.Append("""<div class="filler"></div>""");
        }

        return Document(
            blocks.ToString(),
            $$"""
              .filler { height: 1000px; background: #e8e8e8 }
              {{css}}
              """);
    }

    static string Document(string body, string css) =>
        $$"""
          <!doctype html>
          <html><head><style>
            html, body, div, p, table { margin: 0; padding: 0 }
            body {
              font-family: "Liberation Sans";
              font-size: 16px;
              line-height: 24px;
            }
            {{css}}
          </style></head>
          <body>{{body}}</body></html>
          """;

    static HtmlOptions Options() =>
        new()
        {
            PageWidth = CorpusLayout.PageWidth,
            PageHeight = CorpusLayout.PageHeight,
            Fonts = CorpusRunner.Options().Fonts
        };

    static async Task<int> PageCount(string html)
    {
        using var document = PdfiumDocument.Load(await HtmlConverter.ConvertAsync(html, Options()));
        return document.PageCount;
    }

    static async Task<PngImage> Render(string html, int index)
    {
        using var document = PdfiumDocument.Load(await HtmlConverter.ConvertAsync(html, Options()));
        var png = document.RenderPage(index, new RenderOptions
        {
            Dpi = CorpusLayout.Dpi
        });

        using var stream = new MemoryStream(png);
        return PngDecoder.Decode(stream);
    }

    /// <summary>
    /// The rows carrying the marker fill, in order.
    /// </summary>
    /// <remarks>
    /// A tolerance of eight per channel, because the edges of a filled rectangle are antialiased
    /// and a run of the exact colour would make the answer depend on where those edges landed. The
    /// marker is far from every other colour in these documents, so the tolerance cannot reach one
    /// of them.
    /// </remarks>
    static List<int> MarkedRows(PngImage image)
    {
        var rows = new List<int>();

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var offset = (y * image.Width + x) * 4;
                var rgba = image.Rgba;

                if (Math.Abs(rgba[offset] - marker[0]) <= 8 &&
                    Math.Abs(rgba[offset + 1] - marker[1]) <= 8 &&
                    Math.Abs(rgba[offset + 2] - marker[2]) <= 8)
                {
                    rows.Add(y);
                    break;
                }
            }
        }

        return rows;
    }

    static int FirstInkedRow(PngImage image)
    {
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var offset = (y * image.Width + x) * 4;
                var rgba = image.Rgba;

                if (rgba[offset] < 240 || rgba[offset + 1] < 240 || rgba[offset + 2] < 240)
                {
                    return y;
                }
            }
        }

        return -1;
    }
}
