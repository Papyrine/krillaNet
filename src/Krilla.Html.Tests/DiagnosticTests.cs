/// <summary>
/// Checks on <see cref="HtmlOptions.OnDiagnostic"/>.
///
/// The invariant it exists to carry is that a conversion reporting nothing laid out every
/// construct in the document the way a browser would. That makes the silent cases as much a part
/// of the contract as the reported ones, and worth as many tests: a sink that reports a document
/// which converts correctly is worse than no sink, because it trains the reader to ignore it.
/// </summary>
public class DiagnosticTests
{
    [Test]
    public async Task OrdinaryMarkupReportsNothing()
    {
        // Everything the corpus proves is rendered correctly. Deliberately includes an <hr>, whose
        // inset border arrives from the default stylesheet rather than from the document, and a
        // <small>, whose size the cascade resolves before it reaches the engine.
        var reports = Collect(
            """
            <h1>Heading</h1>
            <p>A paragraph with <b>bold</b>, <i>italic</i> and <a href="#x">a link</a>.</p>
            <hr>
            <ul><li>One</li><li>Two</li></ul>
            <ol start="3"><li>Three</li></ol>
            <blockquote><small>Quoted</small></blockquote>
            <table><tr><th>Head</th></tr><tr><td>Cell</td></tr></table>
            """);

        await Assert.That(reports).IsEmpty();
    }

    /// <summary>
    /// Scenarios that report, and are meant to.
    /// </summary>
    /// <remarks>
    /// Asserted to be STILL reporting, so a construct that becomes fully supported forces its own
    /// removal from this list rather than rotting here.
    /// </remarks>
    static readonly HashSet<string> expectedReports =
    [
        with(StringComparer.OrdinalIgnoreCase),

        // The one construct the corpus deliberately contains that is not fully honoured: CSS says
        // a fixed box repeats on every page and this places it on the one page its position falls
        // on. The scenario is a single page, so the unimplemented half is a no-op there and the
        // geometry is exact — but the report is about the construct, and the reporter cannot know
        // the page count from the cascade.
        "position/fixed"
    ];

    /// <summary>
    /// Every corpus scenario converts without reporting anything.
    /// </summary>
    /// <remarks>
    /// The strongest test of the invariant available here, and the one that keeps the false
    /// positive rate at zero as the table grows. Every scenario has box geometry independently
    /// proven to match Chrome exactly, so a report against one outside
    /// <see cref="expectedReports"/> is a false positive by construction rather than by opinion —
    /// no judgement call about whether the reported construct "really" renders wrongly.
    ///
    /// It found two while it was being written: a <c>&lt;hr&gt;</c> reported four times over a
    /// border style the default stylesheet supplies rather than the document, and a plain
    /// <c>&lt;small&gt;</c> reported a font size the cascade had already resolved correctly.
    /// </remarks>
    [Test]
    public async Task TheCorpusReportsNothing()
    {
        var reports = new List<(string Scenario, HtmlDiagnostic Report)>();
        var reporting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in CorpusLayout.Directories())
        {
            var name = CorpusLayout.Name(directory);
            var options = CorpusRunner.Options(directory);
            options.OnDiagnostic = report =>
            {
                reporting.Add(name);

                if (!expectedReports.Contains(name))
                {
                    reports.Add((name, report));
                }
            };

            HtmlConverter.Convert(CorpusLayout.Html(directory), options);
        }

        await Assert.That(reports).IsEmpty();

        await Assert.That(expectedReports.Except(reporting))
            .IsEmpty()
            .Because("these are listed as expected to report but now report nothing, so they " +
                     "should be removed from the list");
    }

    [Test]
    public async Task UnrecognisedCssIsSilent()
    {
        // Properties the engine has no opinion about. Reporting these would bury the ones it does,
        // which is the whole reason the table is a list rather than a sweep of the cascade.
        var reports = Collect(
            "<p>Text</p>",
            "p { cursor: pointer; content: 'x'; scroll-behavior: smooth; caret-color: red; }");

        await Assert.That(reports).IsEmpty();
    }

    [Test]
    public async Task DisplayNoneIsSilent()
    {
        // A browser draws nothing for it either, so nothing was lost.
        var reports = Collect("<p style=\"display: none\">Hidden</p>");

        await Assert.That(reports).IsEmpty();
    }

    [Test]
    public async Task NoOpValuesAreSilent()
    {
        // What a reset stylesheet writes. Each is the value that makes ignoring the property
        // correct, so each renders exactly as asked.
        var reports = Collect(
            "<p>Text</p>",
            """
            p {
              float: none; clear: none; position: static;
              overflow: visible; visibility: visible; opacity: 1; transform: none;
              text-transform: none; letter-spacing: normal; word-spacing: normal;
            }
            """);

        await Assert.That(reports).IsEmpty();
    }

    [Test]
    public async Task NothingIsBuiltWithoutASink()
    {
        // The scan is skipped rather than run and discarded, so this is really a check that a
        // document full of unsupported css converts without the reporting path throwing.
        var options = CorpusRunner.Options();

        await Assert.That(options.OnDiagnostic).IsNull();

        var pdf = HtmlConverter.Convert(Page("<div style=\"float: left\">Floated</div>", null), options);

        await Assert.That(pdf).IsNotEmpty();
    }

    [Test]
    public Task UnsupportedLayoutIsReported() =>
        Verify(
            Collect(
                """
                <div style="display: flex"><span>a</span></div>
                <div style="display: grid"><span>b</span></div>
                <div style="position: fixed">e</div>
                <table style="border-collapse: collapse"><tr><td>f</td></tr></table>
                <p style="hyphens: auto">g</p>
                <p style="hyphens: manual">h</p>
                """));

    [Test]
    public Task UnsupportedPaintingIsReported() =>
        Verify(
            Collect(
                """
                <div style="border: 2px dashed red">a</div>
                <div style="border-radius: 4px; border: 1px solid red">b</div>
                <div style="opacity: 0.5">c</div>
                <div style="text-decoration: line-through">d</div>
                <div style="text-decoration: overline">e</div>
                <div style="text-transform: full-width">f</div>
                <div style="visibility: collapse">g</div>
                <div style="background-image: linear-gradient(red, blue)">h</div>
                """));

    /// <summary>
    /// What is left of the pagination properties once the forced breaks are honoured.
    /// </summary>
    /// <remarks>
    /// The first three are silent, and are here for that reason rather than despite it: they are
    /// the values this engine now paginates the way a browser does, and a report against one would
    /// be a false positive of exactly the kind the sink exists not to produce. The last three are
    /// the remainder — an <c>avoid</c> at a box edge, which asks for a break to be moved rather
    /// than taken, a value naming a sheet side, whose break is taken and whose side is not, and
    /// the two line-count properties.
    /// </remarks>
    [Test]
    public Task PaginationPropertiesAreReported() =>
        Verify(
            Collect(
                """
                <div style="break-before: page">a</div>
                <div style="page-break-after: always">b</div>
                <div style="break-inside: avoid">c</div>
                <div style="page-break-before: avoid">d</div>
                <div style="break-after: left">e</div>
                <p style="orphans: 3; widows: 3">f</p>
                """));

    [Test]
    public Task PresentationalAttributesAreReported() =>
        Verify(
            Collect(
                """
                <table width="300" cellpadding="8" bgcolor="silver">
                  <tr height="40"><td align="right" valign="bottom" nowrap>a</td></tr>
                </table>
                <ol type="a"><li>b</li></ol>
                <p align="center">c</p>
                <font color="red" size="6">d</font>
                """));

    [Test]
    public Task ColumnsAndUnresolvedImagesAreReported() =>
        Verify(
            Collect(
                """
                <table>
                  <colgroup><col width="200"><col></colgroup>
                  <tr><td>a</td><td>b</td></tr>
                </table>
                <img src="does-not-exist.png" alt="missing">
                """));

    /// <summary>
    /// A font-size keyword falls back to the inherited size, and reports that it did.
    /// </summary>
    /// <remarks>
    /// A regression test with a specific failure behind it: the fallback used to be an absolute
    /// zero, so every keyword — <c>medium</c> and <c>large</c> as much as <c>smaller</c> — resolved
    /// to a font size of 0 and the element rendered as nothing at all. The box height is what
    /// catches that; the report only says the size is not the right one.
    /// </remarks>
    [Test]
    [Arguments("xx-small")]
    [Arguments("small")]
    [Arguments("medium")]
    [Arguments("large")]
    [Arguments("xx-large")]
    [Arguments("smaller")]
    [Arguments("larger")]
    public async Task AFontSizeKeywordKeepsItsText(string keyword)
    {
        var reports = Collect($"<p style=\"font-size: {keyword}\">Text</p>");

        await Assert.That(reports.Select(_ => _.Name)).Contains("font-size");

        var boxes = BoxDump.Measure(
            Page($"<p style=\"font-size: {keyword}\">Text</p>", null),
            CorpusRunner.Options());

        // Not zero, which is what an invisible element measures.
        await Assert.That(boxes[^1].Height).IsGreaterThan(0);
    }

    [Test]
    public async Task InheritIsSilentBecauseTheFallbackIsWhatItAsksFor()
    {
        var reports = Collect("<p style=\"font-size: inherit\">Text</p>");

        await Assert.That(reports).IsEmpty();
    }

    [Test]
    public async Task ADiagnosticReadsAsASentence()
    {
        var reports = Collect("<div style=\"opacity: 0.5\">a</div>");

        var report = reports.Single();

        await Assert.That(report.Kind).IsEqualTo(HtmlDiagnosticKind.UnsupportedProperty);
        await Assert.That(report.Element).IsEqualTo("div");
        await Assert.That(report.Name).IsEqualTo("opacity");
        await Assert.That(report.Value).IsEqualTo("0.5");
        await Assert.That(report.ToString()).IsEqualTo("<div> opacity: 0.5 — painted opaque");
    }

    /// <summary>
    /// A property stops reporting once it is implemented.
    /// </summary>
    /// <remarks>
    /// The table is a list of what the engine gets wrong, so an entry that outlives its bug turns
    /// the sink into noise — and worse, into noise that says a correct document is broken. This is
    /// the check that the removal happens; <c>float</c> earned it by being the first entry ever
    /// removed.
    /// </remarks>
    [Test]
    public async Task ImplementedPropertiesStopReporting()
    {
        var reports = Collect(
            """
            <div style="float: left; width: 50px; height: 20px"></div>
            <p style="clear: left">text</p>
            <div style="position: relative; top: 2px">shifted</div>
            <div style="position: absolute; top: 0; left: 0">out of flow</div>
            <div style="min-height: 40px; max-height: 80px">held open</div>
            <p style="text-indent: 2em">indented</p>
            <span style="display: inline-block; width: 40px">atomic</span>
            <div style="overflow: hidden">clipped</div>
            <div style="visibility: hidden">unpainted</div>
            <span style="visibility: visible">shown again</span>
            <p style="text-transform: uppercase">cased</p>
            <p style="letter-spacing: 2px; word-spacing: 4px">spaced</p>
            """);

        await Assert.That(reports).IsEmpty();
    }

    /// <summary>
    /// The properties that stayed unimplemented and now say so.
    /// </summary>
    /// <remarks>
    /// The other half of <see cref="ImplementedPropertiesStopReporting"/>: a property the engine
    /// reads and does not honour has to be in the table, or a document using it converts wrongly
    /// in silence. These five were found by auditing what the resolver reads against what the
    /// table lists — three were implemented instead, and these two report.
    /// </remarks>
    [Test]
    public async Task SilentlyIgnoredPropertiesReport()
    {
        var reports = Collect(
            """
            <div style="box-shadow: 0 0 4px #000">shadowed</div>
            <table style="caption-side: bottom"><caption>below</caption><tr><td>a</td></tr></table>
            """);

        await Assert.That(reports.Select(_ => _.ToString()))
            .Contains(_ => _.Contains("box-shadow"))
            .And
            .Contains(_ => _.Contains("caption-side"));
    }

    static List<HtmlDiagnostic> Collect(string body, string? css = null)
    {
        var reports = new List<HtmlDiagnostic>();
        var options = CorpusRunner.Options();
        options.OnDiagnostic = reports.Add;

        // Through the whole conversion rather than layout alone, so a report raised while painting
        // would be caught here too.
        HtmlConverter.Convert(Page(body, css), options);
        return reports;
    }

    static string Page(string body, string? css) =>
        $"""
         <!doctype html>
         <html><head><style>{css}</style></head><body>{body}</body></html>
         """;
}
