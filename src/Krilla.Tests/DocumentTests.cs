/// <summary>
/// Behaviour of the document, page and surface lifecycle as seen from managed code.
/// </summary>
public class DocumentTests
{
    static byte[] Pdf(Action<Surface> draw, float width = 100, float height = 100)
    {
        using var document = new KrillaDocument();

        using (var page = document.StartPage(width, height))
        {
            draw(page.Surface);
        }

        return document.Finish();
    }

    [Test]
    public async Task EmptyDocumentIsStillAValidPdf()
    {
        using var document = new KrillaDocument();
        var pdf = document.Finish();

        // krilla writes an empty page rather than a page-less document, which would be
        // invalid PDF.
        await Assert.That(pdf.Length).IsGreaterThan(100);
        await Assert.That(Encoding.ASCII.GetString(pdf, 0, 5)).IsEqualTo("%PDF-");
    }

    [Test]
    public async Task DrawnContentEnlargesTheOutput()
    {
        using var empty = new KrillaDocument();
        var baseline = empty.Finish().Length;

        var drawn = Pdf(_ => _.FillRectangle(new(10, 10, 90, 90), Color.Rgb(200, 0, 0)));

        await Assert.That(drawn.Length).IsGreaterThan(baseline);
    }

    [Test]
    public async Task PageSizeIsRecordedInTheOutput()
    {
        var pdf = Pdf(_ => { }, 200, 400);
        var text = Encoding.Latin1.GetString(pdf);

        // The media box krilla derives from the surface size.
        await Assert.That(text).Contains("200");
        await Assert.That(text).Contains("400");
    }

    [Test]
    public async Task StartingASecondPageWhileOneIsOpenThrows()
    {
        using var document = new KrillaDocument();
        using var first = document.StartPage(100, 100);

        await Assert.That(() => document.StartPage(100, 100)).Throws<KrillaException>();
    }

    [Test]
    public async Task ClosingAPageAllowsTheNextOne()
    {
        using var document = new KrillaDocument();

        using (document.StartPage(100, 100))
        {
        }

        using (document.StartPage(100, 100))
        {
        }

        var pdf = document.Finish();
        await Assert.That(pdf.Length).IsGreaterThan(100);
    }

    [Test]
    public async Task FinishingWithAPageOpenThrows()
    {
        using var document = new KrillaDocument();
        using var page = document.StartPage(100, 100);

        await Assert.That(document.Finish).Throws<KrillaException>();
    }

    [Test]
    public async Task DrawingIntoAClosedPageThrows()
    {
        using var document = new KrillaDocument();
        Surface surface;

        using (var page = document.StartPage(100, 100))
        {
            surface = page.Surface;
        }

        await Assert.That(() => surface.FillRectangle(new(0, 0, 10, 10), Color.Black))
            .Throws<KrillaException>();
    }

    [Test]
    public async Task UsingADocumentAfterFinishThrows()
    {
        using var document = new KrillaDocument();
        document.Finish();

        await Assert.That(() => document.StartPage(100, 100)).Throws<KrillaException>();
    }

    [Test]
    public async Task UsingADocumentAfterDisposeThrows()
    {
        var document = new KrillaDocument();
        document.Dispose();

        await Assert.That(() => document.StartPage(100, 100)).Throws<ObjectDisposedException>();
    }

    [Test]
    [Arguments(0f, 100f)]
    [Arguments(100f, 0f)]
    [Arguments(-10f, 100f)]
    [Arguments(float.NaN, 100f)]
    public async Task DegeneratePageSizeThrows(float width, float height)
    {
        using var document = new KrillaDocument();

        await Assert.That(() => document.StartPage(width, height)).Throws<KrillaException>();
    }

    [Test]
    public async Task DisposingADocumentWithAPageOpenIsClean()
    {
        // krilla asserts an empty push stack inside Surface's destructor, and a panic there
        // would abort the process rather than raise. The shim rebalances first; this proves
        // the managed side survives the same path.
        var document = new KrillaDocument();
        var page = document.StartPage(100, 100);
        page.Surface.FillRectangle(new(0, 0, 50, 50), Color.Black);

        document.Dispose();

        // Building a fresh document afterwards is the real assertion: it proves the process
        // survived and the native library is still in a usable state.
        using var next = new KrillaDocument();
        await Assert.That(next.Finish().Length).IsGreaterThan(100);
    }

    [Test]
    public async Task UnbalancedLayersStillCloseThePage()
    {
        using var document = new KrillaDocument();
        var page = document.StartPage(100, 100);

        // Deliberately leaked: the layer is never disposed.
        page.Surface.PushTransform(Matrix.Translate(10, 10));

        // Closing reports the imbalance rather than aborting.
        await Assert.That(page.Dispose).Throws<KrillaException>();

        // ...and the document survives it.
        var pdf = document.Finish();
        await Assert.That(pdf.Length).IsGreaterThan(100);
    }

    // krilla asserts on stops spanning colour spaces, and an assert is a panic. The shim
    // rejects it up front so it stays a plain argument error rather than poisoning the
    // document.
    [Test]
    public async Task GradientStopsSpanningColourSpacesThrows() =>
        await Assert.That(
                () => Paint.LinearGradient(
                    0, 0, 100, 0,
                    [
                        new(0f, Color.White),
                        new(1f, Color.Rgb(0, 0, 255))
                    ]))
            .Throws<KrillaException>();

    [Test]
    public async Task GradientWithNoStopsThrows() =>
        await Assert.That(() => Paint.LinearGradient(0, 0, 100, 0, []))
            .Throws<ArgumentException>();

    [Test]
    public async Task SaveWritesTheFileToDisk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"krilla-{Guid.NewGuid():N}.pdf");

        try
        {
            using var document = new KrillaDocument();

            using (var page = document.StartPage(PageSettings.A4))
            {
                page.Surface.FillRectangle(new(10, 10, 100, 100), Color.Gray(128));
            }

            document.Save(path);

            await Assert.That(File.Exists(path)).IsTrue();
            await Assert.That(new FileInfo(path).Length).IsGreaterThan(100);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
