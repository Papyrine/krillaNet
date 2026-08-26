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
        var reports = await Collect(
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

        // A row that deliberately names an image which does not exist, to measure the fallback to
        // the counter style. The fallback is exactly what a browser does, so the RENDER is right
        // and the report is still wanted: from the resolved style, an image the policy refused and
        // an image that is simply absent are the same null, and the first is worth hearing about.
        "block/list_image",

        // A row whose four border edges disagree on a colour, which is the one arrangement left
        // where a rounded inline element differs from a browser: with no single ring to draw, the
        // four rectangles are cut to the rounded outline instead, so the OUTER corner rounds and
        // the inner one stays square. Everything else in the scenario is silent.
        "inline/border_radius"
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

            await HtmlConverter.ConvertAsync(CorpusLayout.Html(directory), options);
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
        var reports = await Collect(
            "<p>Text</p>",
            "p { cursor: pointer; content: 'x'; scroll-behavior: smooth; caret-color: red; }");

        await Assert.That(reports).IsEmpty();
    }

    [Test]
    public async Task DisplayNoneIsSilent()
    {
        // A browser draws nothing for it either, so nothing was lost.
        var reports = await Collect("<p style=\"display: none\">Hidden</p>");

        await Assert.That(reports).IsEmpty();
    }

    [Test]
    public async Task NoOpValuesAreSilent()
    {
        // What a reset stylesheet writes. Each is the value that makes ignoring the property
        // correct, so each renders exactly as asked.
        var reports = await Collect(
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

        var pdf = await HtmlConverter.ConvertAsync(Page("<div style=\"float: left\">Floated</div>", null), options);

        await Assert.That(pdf).IsNotEmpty();
    }

    [Test]
    public async Task UnsupportedLayoutIsReported() =>
        await Verify(
            await Collect(
                """
                <div style="display: flex"><span>a</span></div>
                <div style="display: grid"><span>b</span></div>
                <div style="position: fixed">e</div>
                <table style="border-collapse: collapse"><tr><td>f</td></tr></table>
                <p style="hyphens: auto">g</p>
                <p style="hyphens: manual">h</p>
                """));

    /// <summary>
    /// What painting still reports.
    /// </summary>
    /// <remarks>
    /// A bevelled border style used to lead this list. It does not any more: <c>groove</c>,
    /// <c>ridge</c>, <c>inset</c> and <c>outset</c> are drawn in the two shades Chromium draws them
    /// in, so every value <c>border-style</c> takes is now honoured and the whole entry came off the
    /// table. What is left of the borders here is a RADIUS on an edge that is not solid, which is a
    /// different gap — the corner is painted square.
    ///
    /// The second row is the inline half of the same entry, and it narrowed rather than
    /// disappearing: an inline element's corners are rounded now, and what is left is the INSIDE of
    /// one, on the fragment whose edges disagree about a colour and so cannot be drawn as a ring.
    /// </remarks>
    [Test]
    public async Task UnsupportedPaintingIsReported() =>
        await Verify(
            await Collect(
                """
                <div style="border-radius: 4px; border: 2px dashed red">b</div>
                <p><span style="border: 4px solid red; border-right-color: blue; border-radius: 8px">c</span></p>
                <div style="text-transform: full-width">f</div>
                <div style="visibility: collapse">g</div>
                <div style="transform: rotate3d(1, 1, 0, 45deg)">t</div>
                <div style="outline: 2px dashed red">o</div>
                <div style="background-image: url(missing.png)">h</div>
                <div style="background-image: repeating-linear-gradient(red, blue 20px)">i</div>
                """));

    /// <summary>
    /// Every pagination property is silent, because every one of them is honoured.
    /// </summary>
    /// <remarks>
    /// An assertion of ABSENCE, which is what makes it worth having: these values are the ones this
    /// engine paginates the way a browser does, and a report against any of them would be a false
    /// positive of exactly the kind the sink exists not to produce. The list is the history of the
    /// feature — each entry reported at some point and stopped when the value was implemented, and
    /// a value that stops reporting has to be seen to stop. <c>avoid</c> at a box edge was the last
    /// to go, moving a break to the declaring box's own edge rather than taking it where pagination
    /// put it.
    ///
    /// The two line-count properties were named here as well, as honoured only on request because
    /// the reference browser did not implement them. Both halves of that were wrong — the browser
    /// does implement them and they are on by default now — so they have a test of their own below,
    /// which reports for the caller who turned them OFF.
    /// </remarks>
    [Test]
    public async Task PaginationPropertiesAreSilent() =>
        await Assert.That(
                await Collect(
                    """
                    <div style="break-before: page">a</div>
                    <div style="page-break-after: always">b</div>
                    <div style="break-inside: avoid">c</div>
                    <div style="page-break-before: avoid">d</div>
                    <div style="break-after: avoid">e</div>
                    <div style="break-after: left">f</div>
                    """))
            .IsEmpty();

    /// <summary>
    /// <c>orphans</c> and <c>widows</c> report only for a caller who has turned them off.
    /// </summary>
    /// <remarks>
    /// They are honoured by default, so reporting them there would fire on documents that are
    /// laid out exactly as the browser lays them out — the one thing the table must never do. The
    /// switch is the only thing that makes them unhonoured, and it is a document-wide decision
    /// rather than a property of the value, which is why this is not an entry in the table.
    /// </remarks>
    [Test]
    public async Task RunConstraintsReportOnlyWhenTurnedOff()
    {
        const string body = """<p style="orphans: 3; widows: 3">f</p>""";

        await Assert.That(await Collect(body)).IsEmpty();

        var reports = new List<HtmlDiagnostic>();
        var options = CorpusRunner.Options();
        options.OnDiagnostic = reports.Add;
        options.HonourOrphansAndWidows = false;

        await HtmlConverter.ConvertAsync(Page(body, null), options);

        await Assert.That(reports.Select(_ => _.Name)).Contains("orphans");
        await Assert.That(reports.Select(_ => _.Name)).Contains("widows");
    }

    [Test]
    public async Task PresentationalAttributesAreReported() =>
        await Verify(
            await Collect(
                """
                <table width="300" cellpadding="8" bgcolor="silver">
                  <tr height="40"><td align="right" valign="bottom" nowrap>a</td></tr>
                </table>
                <ol type="a"><li>b</li></ol>
                <p align="center">c</p>
                <font color="red" size="6">d</font>
                """));

    [Test]
    public async Task ColumnsAndUnresolvedImagesAreReported() =>
        await Verify(
            await Collect(
                """
                <table>
                  <colgroup><col width="200"><col></colgroup>
                  <tr><td>a</td><td>b</td></tr>
                </table>
                <img src="does-not-exist.png" alt="missing">
                """))
                .Snapshot(
                    """
                    [
                      {
                        "Kind": "UnresolvedImage",
                        "Element": "img",
                        "Name": "src",
                        "Value": "does-not-exist.png",
                        "Reason": "did not resolve to an image, so no box was generated"
                      }
                    ]
                    """);

    /// <summary>
    /// A font-size keyword resolves to a real size, and says nothing about it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A regression test with a specific failure behind it: the fallback used to be an absolute
    /// zero, so every keyword — <c>medium</c> and <c>large</c> as much as <c>smaller</c> — resolved
    /// to a font size of 0 and the element rendered as nothing at all. It then spent a while
    /// falling back to the INHERITED size, which was visible but wrong, and reporting that it had.
    /// </para>
    /// <para>
    /// Checked by measuring the same text against the size the keyword names, which is the only
    /// way to read a font size back out of a box tree. The expected pixel values are the table
    /// measured out of Chrome, so this pins the table itself and not merely that something
    /// happened.
    /// </para>
    /// </remarks>
    [Test]
    [Arguments("xx-small", "9px")]
    [Arguments("x-small", "10px")]
    [Arguments("small", "13px")]
    [Arguments("medium", "16px")]
    [Arguments("large", "18px")]
    [Arguments("x-large", "24px")]
    [Arguments("xx-large", "32px")]
    [Arguments("smaller", "13.3333px")]
    [Arguments("larger", "19.2px")]
    public async Task AFontSizeKeywordResolvesToItsSize(string keyword, string equivalent)
    {
        await Assert.That(await Collect($"<p style=\"font-size: {keyword}\">Text</p>")).IsEmpty();

        await Assert.That(await Height(keyword)).IsGreaterThan(0);
        await Assert.That(await Height(keyword)).IsEqualTo(await Height(equivalent));

        static async Task<float> Height(string size)
        {
            var markup = $"<p style=\"font-size: {size}\">Text</p>";
            var boxes = await BoxDump.MeasureAsync(Page(markup, null), CorpusRunner.Options());

            return boxes[^1].Height;
        }
    }

    [Test]
    public async Task InheritIsSilentBecauseTheFallbackIsWhatItAsksFor()
    {
        var reports = await Collect("<p style=\"font-size: inherit\">Text</p>");

        await Assert.That(reports).IsEmpty();
    }

    [Test]
    public async Task ADiagnosticReadsAsASentence()
    {
        var reports = await Collect("<div style=\"column-count: 3\">a</div>");

        var report = reports.Single();

        await Assert.That(report.Kind).IsEqualTo(HtmlDiagnosticKind.UnsupportedProperty);
        await Assert.That(report.Element).IsEqualTo("div");
        await Assert.That(report.Name).IsEqualTo("column-count");
        await Assert.That(report.Value).IsEqualTo("3");
        await Assert.That(report.ToString())
            .IsEqualTo("<div> column-count: 3 — laid out in one column");
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
        var reports = await Collect(
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
            <p style="font-size: large">sized by keyword</p>
            <p style="text-decoration: underline overline line-through">ruled</p>
            <p>an <sup>exponent</sup> and a <sub>subscript</sub></p>
            <div style="border: 3px dotted red; border-radius: 0">dotted</div>
            <div style="border-radius: 8px; background: silver">rounded</div>
            <div style="opacity: 0.4">faded</div>
            <div style="outline: 2px solid red; outline-offset: 3px">ringed</div>
            <img src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAIAAAB7QOjdAAAAD0lEQVR4nGM8YWPDwMAAAAbaAUIi0kNIAAAAAElFTkSuQmCC" style="object-fit: cover" alt="">
            <table style="caption-side: bottom"><caption>below</caption><tr><td>a</td></tr></table>
            <ul style="list-style-position: inside"><li>inside</li></ul>
            <table style="border-collapse: collapse; border: 2px solid red">
              <tr><td style="border: 1px solid blue">a</td><td>b</td></tr>
            </table>
            <div style="background-image: linear-gradient(to right, red, blue)">ramped</div>
            <div style="background-image: radial-gradient(circle, red, blue)">round</div>
            <div style="transform: rotate(15deg) scale(1.2); transform-origin: top left">turned</div>
            <div style="border: 2px solid rgba(0, 0, 0, 0.4); outline: 1px solid rgba(0, 0, 0, 0.2)">faint</div>
            <div style="box-shadow: inset 4px 4px #000">pressed in</div>
            <ul style="list-style-type: lower-greek"><li>counted in Greek</li></ul>
            <p><span style="background-image: linear-gradient(to right, red, blue)">a ramp on a span</span></p>
            <p style="margin-inline: 4px; inline-size: 100px">logically sized</p>
            <p style="word-wrap: break-word">breakable</p>
            <ul style="list-style-type: '→'"><li>marked with a literal</li></ul>
            <p style="text-decoration: underline; text-decoration-color: rgba(0, 0, 0, 0.3)">faintly ruled</p>
            <p style="height: 40px"><span style="height: 50%">a share of a definite height</span></p>
            <p>A <span style="background: silver; padding: 2px 6px; border-radius: 8px">rounded badge</span>.</p>
            <p>A <span style="border: 2px solid red; border-radius: 8px">rounded frame</span>.</p>
            <div style="break-before: avoid; break-after: avoid">kept with its neighbours</div>
            <table><tr><td style="vertical-align: baseline">on the row's baseline</td></tr></table>
            """);

        await Assert.That(reports).IsEmpty();
    }

    /// <summary>
    /// The properties that stayed unimplemented and now say so.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The other half of <see cref="ImplementedPropertiesStopReporting"/>: a property the engine
    /// reads and does not honour has to be in the table, or a document using it converts wrongly in
    /// silence. Each of these was found by auditing what the resolver reads against what the table
    /// lists, rather than by anything failing.
    /// </para>
    /// <para>
    /// <c>border-style: hidden</c> inside a collapsed table used to be here and is not. It was
    /// documented as unimplementable because the width was folded to zero before anything could tell
    /// it from an absent border, and became a two-line change once the style was kept as its own
    /// value. A translucent border was here too, until the alpha was given somewhere to travel
    /// beside the colour. A property that stops reporting has to be seen to stop, which is what
    /// <see cref="ImplementedPropertiesStopReporting"/> is for.
    /// </para>
    /// </remarks>
    [Test]
    public async Task SilentlyIgnoredPropertiesReport()
    {
        var reports = await Collect(
            """
            <div style="box-shadow: 0 0 4px #000">a blurred shadow</div>
            <div style="column-count: 2">two columns</div>
            <div style="list-style-type: armenian">an unimplemented counter style</div>
            """);

        var lines = reports.Select(_ => _.ToString()).ToList();

        await Assert.That(lines).Contains(_ => _.Contains("box-shadow"));
        await Assert.That(lines).Contains(_ => _.Contains("column-count"));
        await Assert.That(lines).Contains(_ => _.Contains("list-style-type"));
    }

    static async Task<List<HtmlDiagnostic>> Collect(string body, string? css = null)
    {
        var reports = new List<HtmlDiagnostic>();
        var options = CorpusRunner.Options();
        options.OnDiagnostic = reports.Add;

        // Through the whole conversion rather than layout alone, so a report raised while painting
        // would be caught here too.
        await HtmlConverter.ConvertAsync(Page(body, css), options);
        return reports;
    }

    static string Page(string body, string? css) =>
        $"""
         <!doctype html>
         <html><head><style>{css}</style></head><body>{body}</body></html>
         """;
}
