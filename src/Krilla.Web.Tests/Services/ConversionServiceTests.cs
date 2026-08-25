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

    // A4 is taller and narrower than Letter, so the same content paginates differently. This is
    // the only observable difference the paper selector makes, and it would otherwise be possible
    // to wire the dropdown to nothing and have every other test still pass.
    [Test]
    public async Task PaperSizeReachesTheEngine()
    {
        var service = Service();
        var html = "<p>" + string.Join(" ", Enumerable.Repeat("word", 400)) + "</p>";

        var letter = await service.ConvertAsync(html, PaperSize.Letter, 0);
        var a4 = await service.ConvertAsync(html, PaperSize.A4, 0);

        await Assert.That(letter.Pdf.Length).IsNotEqualTo(a4.Pdf.Length);
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
