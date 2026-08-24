/// <summary>
/// The SVG surface, as seen from managed code.
/// </summary>
public class SvgTests
{
    const string Square =
        """<svg xmlns="http://www.w3.org/2000/svg" width="64" height="32"><rect width="64" height="32" fill="red"/></svg>""";

    static PdfSvg Parse(string source, SvgOptions? options = null) =>
        PdfSvg.Load(Encoding.UTF8.GetBytes(source), options);

    static byte[] Pdf(Action<Surface> draw)
    {
        using var document = new KrillaDocument();

        using (var page = document.StartPage(100, 100))
        {
            draw(page.Surface);
        }

        return document.Finish();
    }

    /// <summary>
    /// False only against a native built by hand with <c>--no-default-features</c>, which no
    /// published package can be. A failure here means a stale <c>runtimes/</c> folder.
    /// </summary>
    [Test]
    public async Task ThePackagedNativeSupportsSvg() =>
        await Assert.That(PdfSvg.IsSupported).IsTrue();

    [Test]
    public async Task SizeComesFromTheWidthAndHeightAttributes()
    {
        using var svg = Parse(Square);

        await Assert.That(svg.Width).IsEqualTo(64f);
        await Assert.That(svg.Height).IsEqualTo(32f);
    }

    /// <summary>
    /// A <c>viewBox</c> alone sizes the document, which is what a layout engine needs to get an
    /// aspect ratio out of an SVG that declares no pixel dimensions — the common case for one
    /// meant to scale.
    /// </summary>
    [Test]
    public async Task AViewBoxAloneIsTheIntrinsicSize()
    {
        using var svg = Parse("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 10"/>""");

        await Assert.That(svg.Width).IsEqualTo(20f);
        await Assert.That(svg.Height).IsEqualTo(10f);
    }

    [Test]
    public async Task MalformedDataThrows() =>
        await Assert.That(() => Parse("<svg><unclosed>"))
            .Throws<KrillaException>();

    [Test]
    public async Task DrawingEnlargesTheOutput()
    {
        var baseline = Pdf(_ => { }).Length;

        using var svg = Parse(Square);
        var drawn = Pdf(_ => _.DrawSvg(svg, new(0, 0, 64, 32)));

        await Assert.That(drawn.Length).IsGreaterThan(baseline);
    }

    [Test]
    public async Task OneParsedSvgDrawsIntoManyRectangles()
    {
        using var svg = Parse(Square);

        var pdf = Pdf(
            _ =>
            {
                _.DrawSvg(svg, new(0, 0, 64, 32));
                _.DrawSvg(svg, Rectangle.FromSize(0, 40, 32, 16));
            });

        await Assert.That(pdf.Length).IsGreaterThan(100);
    }

    /// <summary>
    /// The rule <see cref="PdfSvg"/> documents: an <c>&lt;image href&gt;</c> naming a file
    /// resolves to nothing.
    /// </summary>
    /// <remarks>
    /// Differential, because that is the only way to see it: the two documents differ solely in
    /// whether the file the href names exists, so identical output is what says nothing was
    /// read. usvg's stock resolver joins the href to the working directory and embeds whatever
    /// it finds, which would make an SVG from an untrusted document into an arbitrary file read.
    /// </remarks>
    [Test]
    public async Task AFileHrefResolvesToNothing()
    {
        var directory = Path.Combine(Path.GetTempPath(), "krilla-svg-href");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "payload.svg");

        var payload = new StringBuilder("""<svg xmlns="http://www.w3.org/2000/svg" width="64" height="32">""");

        for (var x = 0; x < 200; x++)
        {
            payload.Append($"""<rect x="{x}" y="0" width="1" height="32" fill="blue"/>""");
        }

        payload.Append("</svg>");
        File.WriteAllText(path, payload.ToString());

        try
        {
            var href = path.Replace('\\', '/');

            var present = Render(href);
            var absent = Render($"{href}.does-not-exist");

            await Assert.That(present).IsEqualTo(absent);
        }
        finally
        {
            File.Delete(path);
        }

        static int Render(string href)
        {
            using var svg = Parse(
                $"""
                 <svg xmlns="http://www.w3.org/2000/svg" width="64" height="32">
                   <image href="{href}" width="64" height="32"/>
                 </svg>
                 """);

            return Pdf(_ => _.DrawSvg(svg, new(0, 0, 64, 32))).Length;
        }
    }

    /// <summary>
    /// The other half of the rule: a <c>data:</c> URI is admitted, its bytes already being in
    /// the document. Without this the hardening above would read as "an SVG cannot embed images
    /// at all", which is a much larger restriction than the one intended.
    /// </summary>
    [Test]
    public async Task ADataUriImageIsAdmitted()
    {
        const string pixel =
            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

        using var withImage = Parse(
            $"""
             <svg xmlns="http://www.w3.org/2000/svg" width="64" height="32">
               <image href="{pixel}" width="64" height="32"/>
             </svg>
             """);

        using var without = Parse(Square);

        var drawn = Pdf(_ => _.DrawSvg(withImage, new(0, 0, 64, 32))).Length;
        var plain = Pdf(_ => _.DrawSvg(without, new(0, 0, 64, 32))).Length;

        await Assert.That(drawn).IsGreaterThan(plain);
    }

    [Test]
    public async Task DisposedSvgIsRejected()
    {
        var svg = Parse(Square);
        svg.Dispose();

        await Assert.That(() => Pdf(_ => _.DrawSvg(svg, new(0, 0, 64, 32))))
            .Throws<ObjectDisposedException>();
    }
}
