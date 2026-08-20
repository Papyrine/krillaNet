/// <summary>
/// End-to-end checks that do not need a browser reference.
///
/// The corpus measures fidelity; these check the machinery underneath it works at all. Worth
/// having separately because a corpus scenario failing tells you the layout is wrong, whereas
/// these tell you the font metrics are unreadable or the PDF is empty — a different problem with a
/// different fix, and one that would otherwise present as every scenario being wrong at once.
/// </summary>
public class SmokeTests
{
    [Test]
    public async Task FontMetricsAreRead()
    {
        using var face = FontFace.LoadFile(
            Path.Combine(CorpusLayout.FontsDirectory, "LiberationSans-Regular.ttf"));

        await Assert.That(face.Family).IsEqualTo("Liberation Sans");
        await Assert.That(face.Weight).IsEqualTo(400);
        await Assert.That(face.Italic).IsFalse();
        await Assert.That(face.UnitsPerEm).IsEqualTo(2048f);

        // A glyph must exist for ordinary text, and it must not be .notdef. Getting zero here is
        // the signature of a cmap that was parsed but not understood, which otherwise shows up as
        // a page of blank boxes much further downstream.
        await Assert.That(face.GlyphIndex('A')).IsNotEqualTo((ushort) 0);
        await Assert.That(face.Covers('g')).IsTrue();

        // Liberation Sans is metric-compatible with Arial, whose cap 'A' advance is 1366/2048 em.
        // Checking a known value catches an hmtx read that is plausible but off by an entry.
        await Assert.That(face.Advance('A', 2048)).IsEqualTo(1366f).Within(1f);

        // A proportional font must not report one width for everything.
        await Assert.That(face.Advance('i', 16)).IsLessThan(face.Advance('W', 16));
    }

    [Test]
    public async Task BoldAndItalicResolveToDistinctFaces()
    {
        var fonts = CorpusRunner.Options().Fonts!;

        var regular = fonts.Resolve(["sans-serif"], 400, italic: false);
        var bold = fonts.Resolve(["sans-serif"], 700, italic: false);
        var italic = fonts.Resolve(["sans-serif"], 400, italic: true);

        await Assert.That(regular.Weight).IsEqualTo(400);
        await Assert.That(bold.Weight).IsEqualTo(700);
        await Assert.That(italic.Italic).IsTrue();

        // Bold sets wider than regular. Measured over a sentence rather than one glyph on purpose:
        // Liberation is metric-compatible with Arial, and that compatibility makes many individual
        // advances identical across weights — 'M', 'a', 'e' and 'W' all match exactly — so a
        // single-character check would fail against a perfectly correct font set.
        const string sentence = "The quick brown fox jumps over the lazy dog";
        await Assert.That(Width(bold, sentence)).IsGreaterThan(Width(regular, sentence));
    }

    static float Width(FontFace face, string text)
    {
        var total = 0f;

        foreach (var character in text)
        {
            total += face.Advance(character, 16);
        }

        return total;
    }

    [Test]
    public async Task BoxGeometryFollowsTheBoxModel()
    {
        var boxes = BoxDump.Measure(
            """
            <!doctype html>
            <html><head><style>
              html, body { margin: 0; padding: 0; }
              #a { width: 200px; height: 100px; padding: 10px; border: 5px solid black; margin: 20px; }
            </style></head>
            <body><div id="a"></div></body></html>
            """,
            CorpusRunner.Options());

        var box = boxes.Single(_ => _.Selector.EndsWith("div:nth-child(1)"));

        // The border box is content plus padding plus border: 200 + 20 + 10 across, 100 + 20 + 10
        // down. The margin is outside it, so it moves the box without growing it.
        await Assert.That(box.Width).IsEqualTo(230f).Within(0.01f);
        await Assert.That(box.Height).IsEqualTo(130f).Within(0.01f);
        await Assert.That(box.X).IsEqualTo(20f).Within(0.01f);
        await Assert.That(box.Y).IsEqualTo(20f).Within(0.01f);
    }

    [Test]
    public async Task AdjacentMarginsCollapse()
    {
        var boxes = BoxDump.Measure(
            """
            <!doctype html>
            <html><head><style>
              html, body { margin: 0; padding: 0; }
              div { height: 50px; margin: 30px 0; }
            </style></head>
            <body><div id="a"></div><div id="b"></div></body></html>
            """,
            CorpusRunner.Options());

        var first = boxes.Single(_ => _.Selector.EndsWith("div:nth-child(1)"));
        var second = boxes.Single(_ => _.Selector.EndsWith("div:nth-child(2)"));

        // 30px bottom against 30px top collapses to 30, not 60. The first box's own top margin
        // collapses out through body and html, so it starts at 30 rather than 0.
        await Assert.That(first.Y).IsEqualTo(30f).Within(0.01f);
        await Assert.That(second.Y - first.Y - first.Height).IsEqualTo(30f).Within(0.01f);
    }

    [Test]
    public async Task TextWrapsAtTheContentEdge()
    {
        var options = CorpusRunner.Options();
        var boxes = BoxDump.Measure(
            """
            <!doctype html>
            <html><head><style>
              html, body { margin: 0; padding: 0; }
              p { width: 200px; font: 16px sans-serif; line-height: 20px; margin: 0; }
            </style></head>
            <body><p>The quick brown fox jumps over the lazy dog and keeps on running.</p></body></html>
            """,
            options);

        var paragraph = boxes.Single(_ => _.Selector.EndsWith("p:nth-child(1)"));

        // Wrapping at 200px must produce several 20px lines. An unwrapped paragraph would be
        // exactly one line tall, which is the failure this catches.
        await Assert.That(paragraph.Height).IsGreaterThan(20f);
        await Assert.That(paragraph.Height % 20).IsEqualTo(0f).Within(0.01f);
    }

    [Test]
    public async Task ConversionProducesRenderablePages()
    {
        var pdf = HtmlConverter.Convert(
            """
            <!doctype html>
            <html><head><style>
              html, body { margin: 0; padding: 0; }
              div { background: #c81e1e; width: 300px; height: 200px; }
            </style></head>
            <body><div></div><p>Hello world.</p></body></html>
            """,
            CorpusRunner.Options());

        await Assert.That(pdf.Length).IsGreaterThan(0);

        using var document = PdfiumDocument.Load(pdf);
        await Assert.That(document.PageCount).IsEqualTo(1);

        var png = document.RenderPage(0, new RenderOptions
        {
            Dpi = CorpusLayout.Dpi
        });

        // The page must carry content. A blank page still encodes to a valid PNG, so the check
        // that matters is that more than the background colour made it through.
        await Assert.That(png.Length).IsGreaterThan(0);

        var temporary = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(temporary, png);
            await Assert.That(PageComparison.CountColors(temporary)).IsGreaterThan(4);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
