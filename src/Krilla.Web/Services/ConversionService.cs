namespace Krilla.Web.Services;

/// <summary>The paper a conversion goes onto.</summary>
public enum PaperSize
{
    Letter,
    A4
}

/// <summary>
/// The PDF, and everything the engine had to say while producing it.
/// </summary>
/// <param name="Pdf">The bytes.</param>
/// <param name="Diagnostics">
/// What was recognised in the document and not rendered the way a browser would. Empty is the
/// meaningful case: a conversion that reports nothing laid out every construct faithfully.
/// </param>
/// <param name="Elapsed">How long the conversion took, for the result pane.</param>
public readonly record struct ConversionResult(
    byte[] Pdf,
    IReadOnlyList<HtmlDiagnostic> Diagnostics,
    TimeSpan Elapsed);

/// <summary>
/// Converts HTML to PDF, in the browser.
/// </summary>
public class ConversionService(FontStore fonts)
{
    /// <summary>
    /// Converts <paramref name="html"/>, collecting diagnostics as it goes.
    /// </summary>
    public async Task<ConversionResult> ConvertAsync(
        string html,
        PaperSize paper,
        float margin)
    {
        var set = await fonts.GetAsync();

        var diagnostics = new List<HtmlDiagnostic>();

        var options = paper == PaperSize.A4 ? HtmlOptions.A4 : HtmlOptions.Letter;
        options.Fonts = set;
        options.OnDiagnostic = diagnostics.Add;
        options.WithMargin(margin);

        // Images are the one thing this app cannot resolve. Nothing may reach the network — that
        // is the library's own default and the right one for a page that converts whatever is
        // pasted into it — and there is no local disk to read either, so an <img> that is not a
        // data: URI resolves to nothing and is reported like any other unrenderable construct.
        // A data: URI still works, since its bytes are already in the document.
        options.ImageResolver = _ => null;

        var started = DateTime.UtcNow;

        // Off the UI thread only in the sense that the await lets the "converting" state paint
        // first. The runtime is single-threaded (see the csproj), so the work itself still runs
        // here — this is what keeps the button from appearing to do nothing while it does.
        await Task.Yield();

        var pdf = await HtmlConverter.ConvertAsync(html, options);

        return new(pdf, diagnostics, DateTime.UtcNow - started);
    }
}
