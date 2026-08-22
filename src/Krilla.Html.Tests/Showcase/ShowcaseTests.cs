/// <summary>
/// The readme's specimen page: one hand-written document converted to PDF and rasterised, so the
/// readme can show what the converter produces on a whole page rather than on a fragment.
/// </summary>
/// <remarks>
/// <para>
/// Not a corpus scenario, and deliberately so. A scenario is a controlled comparison — one
/// construct, the shared reset, and a browser reference to measure against — whereas this is a
/// designed page combining most of them at once, which localises nothing when it moves. What it
/// covers instead is the thing no scenario does: that the constructs compose, on a real page, with
/// no reset stylesheet underneath them.
/// </para>
/// <para>
/// The diagnostic assertion is what makes the picture trustworthy. Every unimplemented construct
/// this engine lays out as a plain block still appears on the page, so a showcase could look
/// perfectly convincing while quietly rendering something the way a browser would not. Requiring
/// the conversion to report nothing means the specimen is written entirely in what the engine
/// actually implements.
/// </para>
/// </remarks>
public class ShowcaseTests
{
    static readonly string documentPath = Path.Combine(
        CorpusLayout.ProjectDirectory,
        "Showcase",
        "showcase.html");

    /// <summary>
    /// A fixed timestamp, so the snapshotted PDF stays byte-reproducible.
    /// </summary>
    static readonly DateTimeOffset created = new(2026, 8, 21, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Specimen()
    {
        var reported = new List<string>();

        var options = CorpusRunner.Options();
        options.OnDiagnostic = diagnostic => reported.Add(diagnostic.ToString());
        options.Metadata = new()
        {
            Title = "Krilla.Html — layout specimen",
            Language = "en-GB",
            CreationDate = created
        };

        var pdf = HtmlConverter.Convert(await File.ReadAllTextAsync(documentPath), options);

        await Assert.That(reported)
            .IsEmpty()
            .Because("the specimen must be written in constructs the engine renders the way a " +
                     "browser would, or the readme shows a page that is quietly wrong");

        using var document = PdfiumDocument.Load(pdf);

        // One page, because the readme shows the first one. A specimen that grew onto a second
        // would put half of what it demonstrates somewhere nobody looks.
        await Assert.That(document.PageCount).IsEqualTo(1);

        var targets = new List<Target>
        {
            new("pdf", new MemoryStream(pdf))
        };

        for (var index = 0; index < document.PageCount; index++)
        {
            // 96 dpi renders one device pixel per CSS pixel, so the PNG comes out at the page's
            // own dimensions and the readme can scale it by a known factor.
            var page = document.RenderPage(index, new RenderOptions
            {
                Dpi = CorpusLayout.Dpi
            });

            targets.Add(new("png", new MemoryStream(page), $"page_{index + 1:0000}"));
        }

        await Verify(targets);
    }
}
