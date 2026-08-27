/// <summary>
/// The logical structure tree, which <see cref="HtmlOptions.Tagged"/> asks for.
/// </summary>
/// <remarks>
/// The corpus cannot measure this. A tag tree carries no ink, so the pixel comparison is blind to
/// it and the geometry comparison is too — the same position <c>link/</c> is in, and the same
/// answer: the PDF is read back and what it holds is asserted directly, and the corpus's own
/// numbers staying put is the separate check that adding the tree disturbed no layout.
/// </remarks>
public class TaggedPdfTests
{
    const string document =
        """
        <h1>The heading</h1>
        <p>A paragraph with <b>bold</b> in it.</p>
        <ul><li>One</li><li>Two</li></ul>
        <table>
          <caption>A table</caption>
          <tr><th scope="col">Head</th><td>Cell</td></tr>
        </table>
        <p><img src="../swatch.png" alt="A coloured swatch"></p>
        """;

    static HtmlOptions Options(bool tagged)
    {
        var options = CorpusRunner.Options(
            Path.Combine(CorpusLayout.InputsDirectory, "image", "inline_flow"));
        options.Tagged = tagged;
        return options;
    }

    /// <summary>
    /// The structure a representative document produces, read back out of the PDF.
    /// </summary>
    [Test]
    public async Task TheTreeIsBuiltFromTheDocument() =>
        await Verify(Structure(await HtmlConverter.ConvertAsync(document, Options(tagged: true))));

    /// <summary>
    /// Off by default, and off means no tree at all rather than an empty one.
    /// </summary>
    [Test]
    public async Task NothingIsTaggedByDefault()
    {
        var options = CorpusRunner.Options();

        await Assert.That(options.Tagged).IsFalse();

        var pdf = await HtmlConverter.ConvertAsync(document, options);

        await Assert.That(Structure(pdf)).IsEmpty();
    }

    /// <summary>
    /// Every corpus scenario converts with tagging on.
    /// </summary>
    /// <remarks>
    /// The breadth this feature needs and the snapshot above cannot give. krilla refuses an
    /// unbalanced marked-content span outright, so a page whose painting opens one and never closes
    /// it throws rather than producing a subtly wrong tree — which makes "converts at all" a real
    /// assertion over 150 documents holding floats, tables, page breaks, repeated headers, running
    /// margin boxes and generated content.
    /// </remarks>
    [Test]
    public async Task EveryScenarioConvertsTagged()
    {
        var failures = new List<string>();

        foreach (var directory in CorpusLayout.Directories())
        {
            var options = CorpusRunner.Options(directory);
            options.Tagged = true;

            try
            {
                var pdf = await HtmlConverter.ConvertAsync(CorpusLayout.Html(directory), options);

                if (pdf.Length == 0)
                {
                    failures.Add($"{CorpusLayout.Name(directory)}: produced nothing");
                }
            }
            catch (Exception exception)
            {
                failures.Add($"{CorpusLayout.Name(directory)}: {exception.Message}");
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    /// <summary>
    /// Nothing puts ink on the page from outside a marked-content span.
    /// </summary>
    /// <remarks>
    /// PDF/UA's other half, and the one prose cannot assert: every operator has to be inside either
    /// a structure element or an artifact, and a background painted outside both is content a
    /// reader may read out at a position nobody chose. Asserted over the corpus rather than over
    /// one document, because the sites that had to be bracketed are spread across every phase the
    /// painter has.
    /// </remarks>
    [Test]
    public async Task NothingIsPaintedOutsideASpan()
    {
        var loose = new List<string>();

        foreach (var directory in CorpusLayout.Directories())
        {
            var options = CorpusRunner.Options(directory);
            options.Tagged = true;

            var pdf = await HtmlConverter.ConvertAsync(CorpusLayout.Html(directory), options);

            foreach (var token in PdfContent.Untagged(pdf).Distinct())
            {
                loose.Add($"{CorpusLayout.Name(directory)}: {token}");
            }
        }

        await Assert.That(loose).IsEmpty();
    }

    /// <summary>
    /// Tagging changes no ink.
    /// </summary>
    /// <remarks>
    /// The guarantee the corpus cannot give, because it runs untagged: a structure tree is
    /// bookkeeping, and a page rendered with one has to be the page rendered without. It is worth
    /// asserting because the artifact spans are bracketed THROUGH the painting rather than around
    /// it — an opening that lands on the wrong side of a clip or a transform would move something.
    /// </remarks>
    [Test]
    public async Task TheSamePageIsPainted()
    {
        var differing = new List<string>();

        foreach (var directory in CorpusLayout.Directories())
        {
            var html = CorpusLayout.Html(directory);

            var plain = CorpusRunner.RenderPages(
                await HtmlConverter.ConvertAsync(html, CorpusRunner.Options(directory)));

            var options = CorpusRunner.Options(directory);
            options.Tagged = true;

            var tagged = CorpusRunner.RenderPages(await HtmlConverter.ConvertAsync(html, options));

            if (plain.Count != tagged.Count)
            {
                differing.Add($"{CorpusLayout.Name(directory)}: {plain.Count} pages against {tagged.Count}");
                continue;
            }

            for (var page = 0; page < plain.Count; page++)
            {
                if (!plain[page].SequenceEqual(tagged[page]))
                {
                    differing.Add($"{CorpusLayout.Name(directory)}: page {page + 1}");
                }
            }
        }

        await Assert.That(differing).IsEmpty();
    }

    /// <summary>
    /// An element's own text keeps its place among its children's, and an anchor is a
    /// <c>Link</c> holding both its words and the annotation over them.
    /// </summary>
    /// <remarks>
    /// The two things a tag NAME cannot show, so this reads the <c>/K</c> arrays instead. Reading
    /// order is the whole point of the tree, and a paragraph holding a word in bold used to produce
    /// both halves of its own text and then the bold — which a reader announces as "Before after.
    /// bold".
    /// </remarks>
    [Test]
    public async Task ContentKeepsItsPlaceAmongTheChildren() =>
        await Verify(
            Nesting(
                await HtmlConverter.ConvertAsync(
                    """
                    <p>Before <b>bold</b> after.</p>
                    <p>See <a href="https://example.com">the link</a> now.</p>
                    <ul><li>One</li></ul>
                    """,
                    Options(tagged: true))));

    /// <summary>
    /// A table cell says how far it reaches and which headers describe it.
    /// </summary>
    /// <remarks>
    /// The case a reader most needs told and the one a tag NAME cannot show: every cell in a table
    /// with spans in it is a <c>TD</c> whatever shape it occupies, so a reader meeting one that
    /// covers two rows announces it as an ordinary cell and the row beneath shears sideways.
    /// </remarks>
    [Test]
    public async Task ATableCellCarriesItsShape() =>
        await Verify(
            Details(
                await HtmlConverter.ConvertAsync(
                    """
                    <table summary="Population and area, by region">
                      <tr>
                        <th id="pop" scope="col" abbr="Pop.">Population</th>
                        <th id="area" scope="col">Area</th>
                      </tr>
                      <tr><td headers="pop" rowspan="2">Wide</td><td headers="area">Cell</td></tr>
                      <tr><td colspan="2">Spanning</td></tr>
                    </table>
                    """,
                    Options(tagged: true))));

    /// <summary>
    /// The span written is the one LAYOUT resolved, not the one the attribute declared.
    /// </summary>
    /// <remarks>
    /// <c>rowspan="0"</c> means "to the end of the rows", a number only the grid knows — so an
    /// engine reading the attribute would tell a reader the cell covers no rows at all while the
    /// page shows it covering two.
    /// </remarks>
    [Test]
    public async Task ASpanIsTheOneLayoutResolved()
    {
        var pdf = await HtmlConverter.ConvertAsync(
            """
            <table>
              <tr><td rowspan="0">Tall</td><td>One</td></tr>
              <tr><td>Two</td></tr>
            </table>
            """,
            Options(tagged: true));

        await Assert.That(Details(pdf).Count(_ => _.Contains("/RowSpan 2"))).IsEqualTo(1);
    }

    /// <summary>
    /// A <c>headers</c> reference that resolves to nothing produces no association.
    /// </summary>
    /// <remarks>
    /// PDF resolves <c>/Headers</c> through the document's id tree, so a reference to an id
    /// nothing published lands a reader on nowhere — which is worse than the association being
    /// absent, since without one a reader falls back to the cell's own column.
    /// </remarks>
    [Test]
    public async Task AnUnresolvedHeaderIsDropped()
    {
        var pdf = await HtmlConverter.ConvertAsync(
            """
            <table>
              <tr><th id="real">Head</th></tr>
              <tr><td headers="real nowhere">Cell</td></tr>
            </table>
            """,
            Options(tagged: true));

        await Assert.That(Details(pdf).Count(_ => _.Contains("/Headers"))).IsEqualTo(1);
        await Assert.That(Details(pdf).Any(_ => _.Contains("nowhere"))).IsFalse();
    }

    /// <summary>
    /// <c>role</c> and the ARIA labels outrank what the element itself says.
    /// </summary>
    /// <remarks>
    /// A document generated by a framework says <c>role="heading"</c> far more often than it
    /// reaches for <c>&lt;h2&gt;</c>, and a tree reading only the element names describes such a
    /// document as a stack of anonymous divisions — which is the one shape a reader cannot
    /// navigate.
    /// </remarks>
    [Test]
    public async Task AriaOutranksTheElement() =>
        await Verify(
            Details(
                await HtmlConverter.ConvertAsync(
                    """
                    <div role="heading" aria-level="3">A heading</div>
                    <div role="list"><div role="listitem">An item</div></div>
                    <p aria-label="The label">Words</p>
                    <p aria-labelledby="named">More</p>
                    <p aria-describedby="named">Described</p>
                    <p aria-label="Both" aria-describedby="named">Named and described</p>
                    <p><abbr title="Portable Document Format">PDF</abbr></p>
                    <span id="named">Elsewhere</span>
                    """,
                    Options(tagged: true))));

    /// <summary>
    /// <c>role="presentation"</c> takes a cell's own semantics away, span included.
    /// </summary>
    /// <remarks>
    /// krilla refuses a span on anything that is not a cell, so this is the arrangement that
    /// throws if the shape is decided from the element's name rather than from the tag the role
    /// actually produced.
    /// </remarks>
    [Test]
    public async Task APresentationalCellCarriesNoSpan()
    {
        var pdf = await HtmlConverter.ConvertAsync(
            """<table><tr><td role="presentation" colspan="2">Plain</td></tr></table>""",
            Options(tagged: true));

        await Assert.That(Structure(pdf)).Contains("NonStruct");
        await Assert.That(Details(pdf).Any(_ => _.Contains("/ColSpan"))).IsFalse();
    }

    /// <summary>
    /// A counter marker is the item's <c>Lbl</c>; a bullet stays an artifact.
    /// </summary>
    /// <remarks>
    /// A reader announcing "3" before an item is saying which item it is, where announcing
    /// "bullet" would only repeat what the list's own tag already said — so the two markers reach
    /// the tree differently even though the painter draws both in the same place.
    /// </remarks>
    [Test]
    public async Task ACounterMarkerIsTheItemsLabel()
    {
        var pdf = await HtmlConverter.ConvertAsync(
            "<ol><li>One</li><li>Two</li></ol><ul><li>Three</li></ul>",
            Options(tagged: true));

        var nesting = Nesting(pdf);

        await Assert.That(nesting.Count(_ => _ == "LI [Lbl, LBody]")).IsEqualTo(2);
        await Assert.That(nesting.Count(_ => _ == "LI [LBody]")).IsEqualTo(1);
    }

    /// <summary>
    /// A marker with no item around it still reaches the tree.
    /// </summary>
    /// <remarks>
    /// Two arrangements produce one, and both would otherwise leave marked content on the page
    /// that no structure element references — the one thing tagging exists to make impossible. An
    /// EMPTY item has a number and nothing to put in an <c>LBody</c>; an item whose <c>role</c>
    /// took its item-ness away has no <c>LI</c> for a <c>Lbl</c> to hang from.
    /// </remarks>
    [Test]
    public async Task AMarkerWithoutAnItemIsStillContent()
    {
        var pdf = await HtmlConverter.ConvertAsync(
            """
            <ol><li></li></ol>
            <ol><li role="presentation">Plain</li></ol>
            """,
            Options(tagged: true));

        await Assert.That(Nesting(pdf)).Contains("LI [Lbl]");
        await Assert.That(Nesting(pdf)).Contains("NonStruct [text, text]");
        await Assert.That(PdfContent.Untagged(pdf)).IsEmpty();
    }

    /// <summary>
    /// A <c>position: fixed</c> box is content once and an artifact on every sheet after.
    /// </summary>
    /// <remarks>
    /// It is drawn on every page — CSS 2.1 §9.6.1, which <c>page/fixed_repeat</c> measures in
    /// pixels — and a reader must meet it once. A repeated table header reaches the same rule
    /// through a path that suppresses tagging wholesale; a fixed box goes through the ordinary
    /// walk on every page, so it needs the count asserting rather than reasoning about.
    /// </remarks>
    [Test]
    public async Task AFixedBoxIsTaggedOnce()
    {
        var html =
            """
            <div style="position: fixed; top: 0; left: 0">Running</div>
            <p style="height: 2000px">Tall</p>
            """;

        var pdf = await HtmlConverter.ConvertAsync(html, Options(tagged: true));

        await Assert.That(CorpusRunner.RenderPages(pdf).Count).IsGreaterThan(1);

        // One span for the box's one line, whatever the page count.
        await Assert.That(Nesting(pdf).Count(_ => _.StartsWith("Div ["))).IsEqualTo(1);
        await Assert.That(Nesting(pdf)).Contains("Div [text]");
    }

    /// <summary>
    /// The document's language reaches the tree, which PDF/UA requires and which nothing on the
    /// page shows.
    /// </summary>
    [Test]
    public async Task TheLanguageReachesTheTree()
    {
        var pdf = await HtmlConverter.ConvertAsync(
            "<html lang=\"en-GB\"><body><p>Text</p></body></html>",
            Options(tagged: true));

        await Assert.That(Text(pdf)).Contains("/Lang");
    }

    /// <summary>
    /// The structure tags a PDF holds, in the order they appear in its objects.
    /// </summary>
    /// <remarks>
    /// Read out of the raw bytes rather than through PDFium, which exposes no structure tree at
    /// all. Crude, and sufficient: what is being asserted is which roles the document produced and
    /// how many of each, and a tag's name is the one thing that appears literally in the file.
    /// </remarks>
    static List<string> Structure(byte[] pdf)
    {
        var found = new List<string>();

        foreach (Match match in Regex.Matches(Text(pdf), @"/S\s*/(\w+)"))
        {
            found.Add(match.Groups[1].Value);
        }

        return found;
    }

    /// <summary>
    /// Every structure element as the properties it carries, with the plumbing removed.
    /// </summary>
    /// <remarks>
    /// What each element SAYS about itself, which is a different question from the shape of the
    /// tree — so the parent, the children and the page they landed on come out, leaving the role,
    /// the id, the alternative text and the attribute dictionary a cell's span and headers live in.
    /// </remarks>
    static List<string> Details(byte[] pdf)
    {
        var found = new List<string>();

        foreach (Match match in Regex.Matches(
                     Text(pdf),
                     @"/Type\s*/StructElem(?<body>.*?)>>\s*endobj",
                     RegexOptions.Singleline))
        {
            var body = match.Groups["body"].Value;

            body = Regex.Replace(body, @"/(P|Pg)\s+\d+ 0 R", "");
            body = Regex.Replace(body, @"/K\s*\[[^\]]*\]", "");

            found.Add(Regex.Replace(body, @"\s+", " ").Trim());
        }

        return found;
    }

    /// <summary>
    /// Every structure element as its role and the roles of what hangs under it, in order.
    /// </summary>
    /// <remarks>
    /// A marked-content id is written as <c>text</c> and an annotation reference as <c>link</c>:
    /// what is being asserted is the SEQUENCE, and the numbers themselves are an accident of how
    /// many spans the painter opened before this one.
    /// </remarks>
    static List<string> Nesting(byte[] pdf)
    {
        var text = Text(pdf);
        var roles = new Dictionary<string, string>(StringComparer.Ordinal);
        var bodies = new List<(string Number, string Role, string Children)>();

        foreach (Match match in Regex.Matches(
                     text,
                     @"(\d+) 0 obj\s*<<(?<body>.*?)>>\s*endobj",
                     RegexOptions.Singleline))
        {
            var body = match.Groups["body"].Value;

            if (Regex.Match(body, @"/S\s*/(\w+)") is not {Success: true} role ||
                Regex.Match(body, @"/K\s*\[(?<items>.*?)\]") is not {Success: true} kids)
            {
                continue;
            }

            roles[match.Groups[1].Value] = role.Groups[1].Value;
            bodies.Add((match.Groups[1].Value, role.Groups[1].Value, kids.Groups["items"].Value));
        }

        var found = new List<string>();

        foreach (var (_, role, children) in bodies)
        {
            var items = new List<string>();

            // An annotation reference is a dictionary of its own, holding object references that
            // are not children — so it is folded to one token before anything counts them.
            var flattened = Regex.Replace(children, "<<.*?>>", " OBJR ", RegexOptions.Singleline);

            foreach (Match item in Regex.Matches(flattened, @"(?<ref>\d+) 0 R|OBJR|(?<mcid>\d+)"))
            {
                if (item.Value == "OBJR")
                {
                    items.Add("link");
                }
                else if (item.Groups["ref"].Success)
                {
                    items.Add(roles.GetValueOrDefault(item.Groups["ref"].Value, "?"));
                }
                else
                {
                    items.Add("text");
                }
            }

            found.Add($"{role} [{string.Join(", ", items)}]");
        }

        return found;
    }

    static string Text(byte[] pdf) =>
        Encoding.Latin1.GetString(pdf);
}

