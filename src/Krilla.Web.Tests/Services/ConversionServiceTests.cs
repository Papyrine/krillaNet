using System.Globalization;
using System.Text.RegularExpressions;

namespace Krilla.Web.Tests.Services;

/// <summary>
/// The conversion path, on the desktop runtime.
/// </summary>
/// <remarks>
/// These run against the same <see cref="ConversionService"/> the app uses, with the same
/// <see cref="FontStore"/> fetching over the same HttpClient — only the transport is local. What
/// they cannot see is whether the native links into a .wasm module, which is what the Playwright
/// tests are for.
/// </remarks>
public class ConversionServiceTests
{
    static ConversionService Service() =>
        new(new(new(new LocalAssetHandler())
        {
            BaseAddress = new("http://localhost/")
        }));

    [Test]
    public async Task ConvertsToPdf()
    {
        var result = await Service().ConvertAsync("<h1>Hello</h1>", PaperSize.Letter, 48);

        await Assert.That(result.Pdf.Length).IsGreaterThan(0);
        // Cheap and specific: a PDF that is not a PDF is the failure worth catching here.
        await Assert.That(Encoding.ASCII.GetString(result.Pdf, 0, 5)).IsEqualTo("%PDF-");
    }

    // The whole point of the diagnostics pane. An ordinary document reports nothing, which is the
    // engine's own invariant — a conversion that reports nothing laid out every construct the way
    // a browser would — so a test that only ever saw reports would be measuring the wrong half.
    [Test]
    public async Task OrdinaryDocumentReportsNothing()
    {
        var result = await Service().ConvertAsync(
            "<h1>Title</h1><p>Some <b>bold</b> text and a <a href='#x'>link</a>.</p>",
            PaperSize.Letter,
            48);

        await Assert.That(result.Diagnostics).IsEmpty();
    }

    [Test]
    public async Task UnsupportedCssIsReported()
    {
        var result = await Service().ConvertAsync(
            "<div style='display: flex'><span>a</span><span>b</span></div>",
            PaperSize.Letter,
            48);

        await Assert.That(result.Diagnostics).IsNotEmpty();
        await Assert.That(result.Diagnostics.Any(_ => _.Name == "display")).IsTrue();
    }

    // Every paper, not a sample of them: the picker is a list, and a size wired to nothing looks
    // exactly like one wired correctly until something measures the page it produced. Reading the
    // width back out of the PDF is what makes this an assertion about the paper rather than about
    // the file merely differing.
    [Test]
    [MethodDataSource(nameof(Papers))]
    public async Task PaperReachesTheEngine(Paper paper)
    {
        var result = await Service().ConvertAsync("<p>x</p>", paper.Size, 0);

        // Read straight out of the PDF rather than through a renderer. The page box is the one
        // thing a paper size has to produce, /MediaBox is where a PDF states it, and pulling in
        // pdfium to learn it would put twelve RIDs of native into a project that also has a
        // WebAssembly app to publish.
        var media = Regex.Match(
            Encoding.Latin1.GetString(result.Pdf),
            @"/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]");

        await Assert.That(media.Success).IsTrue();

        // The engine measures in CSS pixels at 96 to the inch and a PDF is in points at 72, so the
        // page comes back scaled by the ratio the converter applies on the way out.
        var width = double.Parse(media.Groups[1].Value, CultureInfo.InvariantCulture);
        var height = double.Parse(media.Groups[2].Value, CultureInfo.InvariantCulture);

        await Assert.That(width).IsEqualTo(paper.Width / HtmlOptions.PixelsPerPoint).Within(0.5);
        await Assert.That(height).IsEqualTo(paper.Height / HtmlOptions.PixelsPerPoint).Within(0.5);
    }

    public static IEnumerable<Func<Paper>> Papers()
    {
        foreach (var paper in Krilla.Web.Services.Papers.All)
        {
            yield return () => paper;
        }
    }

    // Every entry distinct, and every label carrying its dimensions. A duplicated size in the table
    // would give the picker two rows that convert identically, which nothing else here would catch.
    [Test]
    public async Task EveryPaperIsDistinct()
    {
        var all = Krilla.Web.Services.Papers.All;

        await Assert.That(all.Select(_ => _.Size).Distinct().Count()).IsEqualTo(all.Count);
        await Assert.That(all.Select(_ => (_.Width, _.Height)).Distinct().Count()).IsEqualTo(all.Count);
        await Assert.That(all.All(_ => _.Label.Contains('×'))).IsTrue();
    }

    // Landscape is not offered, so every sheet is taller than it is wide; a transposed row in the
    // table would otherwise be invisible until somebody converted onto it.
    [Test]
    public async Task EveryPaperIsPortrait()
    {
        foreach (var paper in Krilla.Web.Services.Papers.All)
        {
            await Assert.That(paper.Height).IsGreaterThan(paper.Width);
        }
    }

    // Images cannot be fetched from a page that converts whatever is pasted into it, so an <img>
    // resolves to nothing and is reported rather than silently dropped. Pins the resolver the
    // service installs; removing it would let the library's default start reading local files.
    [Test]
    public async Task RemoteImageIsNotFetched()
    {
        var result = await Service().ConvertAsync(
            "<img src='https://example.com/logo.png' width='10' height='10'>",
            PaperSize.Letter,
            48);

        await Assert.That(result.Diagnostics.Any(_ => _.Kind == HtmlDiagnosticKind.UnresolvedImage)).IsTrue();
    }

    [Test]
    public async Task SampleDocumentConverts()
    {
        var html = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "sample.html"));

        var result = await Service().ConvertAsync(html, PaperSize.Letter, 48);

        await Assert.That(Encoding.ASCII.GetString(result.Pdf, 0, 5)).IsEqualTo("%PDF-");
    }
}
