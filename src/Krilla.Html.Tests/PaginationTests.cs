/// <summary>
/// Forced page breaks, in the arrangements the corpus cannot reach.
///
/// The three <c>page/break_*</c> scenarios measure what a browser does with a break in the shape
/// an author writes most often: one declaration, between two siblings, in the middle of a
/// document. The rules below are the edges of that — a break asked for where there is nothing to
/// move, a break on a box that is not at a flow position, a box that asks to stay whole and cannot
/// fit. None of them has a browser reference, because each is about a page NOT appearing, and a
/// scenario whose expected output is "no extra page" measures nothing against a reference that
/// also has none.
///
/// Page counts throughout. It is the sharpest available signal for this feature: a forced break is
/// the one thing in <see cref="Paginator"/> that adds a page rather than moving one, so a rule
/// applied when it should not be, or skipped when it should not be, changes the count by exactly
/// one.
/// </summary>
public class PaginationTests
{
    [Test]
    public async Task ForcedBreakAddsAPageToContentThatFits()
    {
        // Three 48px boxes on a 1056px page. Nothing here would paginate on its own.
        await Assert.That(PageCount(Three(""))).IsEqualTo(1);
        await Assert.That(PageCount(Three("#two { page-break-before: always }"))).IsEqualTo(2);
        await Assert.That(PageCount(Three("#one { page-break-after: always }"))).IsEqualTo(2);
    }

    [Test]
    public async Task ForcedBreaksAccumulate() =>
        await Assert.That(PageCount(Three("#two, #three { page-break-before: always }")))
            .IsEqualTo(3);

    /// <summary>
    /// Both spellings reach the resolver, and reach the same answer.
    /// </summary>
    /// <remarks>
    /// The cascade does not alias them — a <c>page-break-before</c> declaration comes back under
    /// that name and nothing at all comes back under <c>break-before</c> — so reading one and not
    /// the other is a defect that halves the documents the feature works on while leaving every
    /// scenario written in the spelling that was read passing.
    /// </remarks>
    [Test]
    public async Task LegacyAndModernSpellingsAgree()
    {
        await Assert.That(PageCount(Three("#two { break-before: page }"))).IsEqualTo(2);
        await Assert.That(PageCount(Three("#two { page-break-before: always }"))).IsEqualTo(2);
        await Assert.That(PageCount(Three("#one { break-after: page }"))).IsEqualTo(2);
        await Assert.That(PageCount(Three("#one { page-break-after: always }"))).IsEqualTo(2);
    }

    /// <summary>
    /// A break asking for the page that already exists produces no blank one.
    /// </summary>
    /// <remarks>
    /// The likely shape of this mistake rather than an exotic case: <c>page-break-before</c> on a
    /// section wrapper is written to separate the sections, and the rule matches the first section
    /// as readily as the rest. A leading blank page is also the kind of defect that survives
    /// review, because the document after it is entirely correct.
    /// </remarks>
    [Test]
    public async Task BreakBeforeOnTheFirstBoxAddsNoBlankPage() =>
        await Assert.That(PageCount(Three("#one { page-break-before: always }"))).IsEqualTo(1);

    /// <summary>
    /// A break after the last box has nothing to move, and so takes no break.
    /// </summary>
    /// <remarks>
    /// A browser emits a trailing blank page here. This deliberately does not: a blank page at the
    /// end of a converted document is the less useful of the two answers, and the same rule that
    /// produces it — <c>page-break-after</c> on every section — is the one that puts the break
    /// between the sections where it is wanted.
    /// </remarks>
    [Test]
    public async Task BreakAfterOnTheLastBoxAddsNoBlankPage() =>
        await Assert.That(PageCount(Three("#three { page-break-after: always }"))).IsEqualTo(1);

    /// <summary>
    /// A break inside a wrapper is a break at that box's own top, not at the wrapper's.
    /// </summary>
    [Test]
    public async Task BreakBeforeIsHonouredBelowTheRoot()
    {
        var html = Document(
            """
            <div id="wrap">
              <div id="first">one</div>
              <div id="second">two</div>
            </div>
            """,
            "#second { page-break-before: always }");

        await Assert.That(PageCount(html)).IsEqualTo(2);

        // #second is 48px down inside the wrapper, so a break taken at the WRAPPER's top would be
        // a break at zero and produce one page. Page two opening with content flush at its top
        // edge is what says the break landed on the inner box.
        await Assert.That(FirstInkedRow(Render(html, 1))).IsEqualTo(0);
    }

    /// <summary>
    /// A <c>break-after</c> on a nested last child resolves to the next box OUTSIDE the wrapper.
    /// </summary>
    /// <remarks>
    /// The case that makes "the next in-flow box in document order" more than a restatement of
    /// "the next sibling". <c>#inner</c> has no next sibling, so the break belongs to whatever
    /// follows its wrapper — and the wrapper's bottom padding puts twenty pixels between the two,
    /// which is what makes a break taken at <c>#inner</c>'s own bottom edge visible rather than
    /// indistinguishable.
    /// </remarks>
    [Test]
    public async Task BreakAfterOnANestedLastChildResolvesPastTheWrapper()
    {
        var html = Document(
            """
            <div id="wrap"><div id="inner">one</div></div>
            <div id="after">two</div>
            """,
            """
            /* No background: the wrapper must not ink, or the twenty pixels this measures would be
               filled by the wrapper on either reading. */
            #wrap { padding-bottom: 20px }
            #inner { page-break-after: always }
            """);

        await Assert.That(PageCount(html)).IsEqualTo(2);

        // #inner ends at 48 and #after begins at 68. A break at #inner's bottom edge starts page
        // two twenty pixels above #after, and the box appears twenty rows down.
        await Assert.That(FirstInkedRow(Render(html, 1))).IsEqualTo(0);
    }

    /// <summary>
    /// <c>avoid</c> at a box edge forces nothing.
    /// </summary>
    /// <remarks>
    /// It asks for a break to be moved rather than taken, and is reported rather than honoured.
    /// Reading it as a forced break would be worse than ignoring it: a page break at every edge an
    /// author asked to keep clear is the exact opposite of what was written.
    /// </remarks>
    [Test]
    public async Task AvoidAtABoxEdgeForcesNothing()
    {
        await Assert.That(PageCount(Three("#two { page-break-before: avoid }"))).IsEqualTo(1);
        await Assert.That(PageCount(Three("#two { break-after: avoid }"))).IsEqualTo(1);
    }

    /// <summary>
    /// A break on an out-of-flow box names no position a page could start at, and is ignored.
    /// </summary>
    /// <remarks>
    /// An absolutely positioned box is placed against its containing block rather than at a flow
    /// position, and a float is placed beside the flow rather than in it. CSS excludes both for
    /// the same reason, and <see cref="Paginator"/> reaches the same answer structurally, by
    /// walking only <c>Children</c> — worth pinning, since a walk that later picks up
    /// <c>Floats</c> or <c>Positioned</c> for some other purpose would silently change this.
    /// </remarks>
    [Test]
    public async Task BreakOnAnOutOfFlowBoxIsIgnored()
    {
        var absolute = Document(
            """
            <div id="one">one</div>
            <div id="floating">two</div>
            """,
            "#floating { position: absolute; top: 200px; page-break-before: always }");

        await Assert.That(PageCount(absolute)).IsEqualTo(1);

        var floated = Document(
            """
            <div id="one">one</div>
            <div id="floating">two</div>
            """,
            "#floating { float: left; width: 100px; page-break-before: always }");

        await Assert.That(PageCount(floated)).IsEqualTo(1);
    }

    /// <summary>
    /// A box asking to stay whole that cannot fit on a page overflows rather than moving forever.
    /// </summary>
    /// <remarks>
    /// The termination case, and the reason it is worth a test of its own: <c>break-inside</c>
    /// makes a box an unbreakable unit, and <see cref="Paginator"/> answers a straddling unit by
    /// moving it to the next page. A unit taller than the page still would not fit there, so
    /// without the existing height guard the search would move it again, and again. It is the same
    /// guard a too-tall table row uses, reached by a route that did not exist before.
    /// </remarks>
    [Test]
    public async Task AvoidOnABoxTallerThanThePageOverflows()
    {
        var html = Document(
            """<div id="tall"></div>""",
            "#tall { height: 3000px; background: #204060; page-break-inside: avoid }");

        // 3000px over 1056px pages, broken at the page edge each time because nothing inside can
        // be moved: three pages, and a run that returns at all.
        await Assert.That(PageCount(html)).IsEqualTo(3);
    }

    /// <summary>
    /// The document body every case here shares: three 48px boxes, with the scenario's own CSS.
    /// </summary>
    static string Three(string css) =>
        Document(
            """
            <div id="one">one</div>
            <div id="two">two</div>
            <div id="three">three</div>
            """,
            css);

    static string Document(string body, string css) =>
        $$"""
          <!doctype html>
          <html><head><style>
            html, body, div, p { margin: 0; padding: 0 }
            div {
              width: 500px;
              padding: 12px;
              font-family: "Liberation Sans";
              font-size: 16px;
              line-height: 24px;
              background: #c81e1e;
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

    static int PageCount(string html)
    {
        using var document = PdfiumDocument.Load(HtmlConverter.Convert(html, Options()));
        return document.PageCount;
    }

    static PngImage Render(string html, int index)
    {
        using var document = PdfiumDocument.Load(HtmlConverter.Convert(html, Options()));
        var png = document.RenderPage(index, new RenderOptions
        {
            Dpi = CorpusLayout.Dpi
        });

        using var stream = new MemoryStream(png);
        return PngDecoder.Decode(stream);
    }

    static int FirstInkedRow(PngImage image)
    {
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var offset = (y * image.Width + x) * 4;
                var rgba = image.Rgba;

                // A loose threshold, like PageMarginTests': the edge of a filled rectangle is
                // antialiased, so demanding the exact colour would make the answer depend on where
                // that edge landed.
                if (rgba[offset] < 240 || rgba[offset + 1] < 240 || rgba[offset + 2] < 240)
                {
                    return y;
                }
            }
        }

        return -1;
    }
}
