namespace Krilla.Html;

/// <summary>
/// Converts HTML to PDF. The entry point to the library.
/// </summary>
/// <remarks>
/// <para>
/// Three stages, each independently inspectable: AngleSharp parses the markup and runs the CSS
/// cascade, <see cref="Layout"/> turns the styled tree into positioned boxes, and
/// <see cref="Painting.PdfPainter"/> draws those boxes through krilla. The middle stage is the one
/// with all the specification in it, and <see cref="LayoutDocument"/> exposes it directly so it
/// can be measured without going near a PDF.
/// </para>
/// <para>
/// What is implemented: block and inline layout, the box model, collapsing margins, line breaking,
/// text alignment, and pagination. What is not, and lays out as a plain block instead: floats,
/// positioned boxes, flexbox, grid and tables.
/// </para>
/// </remarks>
public static class HtmlConverter
{
    /// <summary>
    /// Converts <paramref name="html"/> to PDF bytes.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="HtmlOptions.Fonts"/> was not set, or holds no faces.
    /// </exception>
    public static byte[] Convert(string html, HtmlOptions options)
    {
        var document = Parse(html, options);
        return Convert(document, options);
    }

    /// <summary>
    /// Converts an already-parsed document to PDF bytes.
    /// </summary>
    /// <remarks>
    /// For a caller who needs the DOM as well as the PDF, so the document is not parsed twice.
    /// </remarks>
    public static byte[] Convert(IDocument document, HtmlOptions options)
    {
        // The layout holds the decoded images, so it has to outlive painting — they are decoded
        // on first draw, not during layout.
        using var layout = LayoutDocument(document, options);
        var root = layout.Root;

        using var pdf = new KrillaDocument();

        if (options.Metadata is {} metadata)
        {
            pdf.SetMetadata(metadata);
        }

        var scale = 1 / HtmlOptions.PixelsPerPoint;
        var content = new Rect(
            options.MarginLeft,
            options.MarginTop,
            options.ContentWidth,
            options.ContentHeight);

        var tops = Paginator.PageTops(root, options.ContentHeight);

        // After pagination, because a fragment names an element while a PDF internal link names a
        // page and a point on it — and which page an element landed on is what pagination decides.
        var links = LinkTargets.Build(root, tops, content, scale);

        for (var index = 0; index < tops.Count; index++)
        {
            using var page = pdf.StartPage(
                options.PageWidth * scale,
                options.PageHeight * scale);

            // Each page runs to where the next one begins, which is a line boundary rather than
            // the bottom of the paper. The last page runs to infinity so nothing is trimmed off
            // the end of the document.
            var end = index + 1 < tops.Count ? tops[index + 1] : float.PositiveInfinity;

            PdfPainter.Paint(page.Surface, root, tops[index], end, content, scale, links);
        }

        return pdf.Finish();
    }

    /// <summary>
    /// Runs the cascade and layout, returning the positioned box tree without painting anything.
    /// </summary>
    /// <remarks>
    /// The seam the corpus comparison hangs off: geometry can be compared to a browser's
    /// <c>getBoundingClientRect()</c> without rasterising anything, which makes the comparison
    /// exact rather than subject to how two rasterisers antialias an edge.
    /// </remarks>
    internal static LayoutResult LayoutDocument(IDocument document, HtmlOptions options)
    {
        var fonts = RequireFonts(options);

        var element = document.DocumentElement ??
                      throw new InvalidOperationException("The document has no root element.");

        var initial = new ComputedStyle
        {
            FontSize = options.RootFontSize,
            FontFamilies = []
        };

        var context = DocumentContext.For(document, options);
        var root = BoxBuilder.Build(element, initial, context);

        // The root box's containing block is the page's content area, which is the print
        // equivalent of the browser's initial containing block. Laid out at the root's own top
        // margin — margins never collapse out of the root, and nothing above it would apply them
        // if they did. Everything stays document-relative from there; page margins are applied
        // once, when painting.
        var top = root.Style.MarginTop.Resolve(options.ContentWidth);
        BlockLayout.Layout(root, 0, top, options.ContentWidth, fonts);
        return new(root, context);
    }

    /// <summary>
    /// Parses <paramref name="html"/> with the CSS cascade enabled.
    /// </summary>
    internal static IDocument Parse(string html, HtmlOptions options)
    {
        var configuration = Configuration.Default
            .WithCss()
            // The render device is what media queries are evaluated against. Setting it to the
            // page means `@media (max-width: ...)` resolves against the paper the document is
            // going onto rather than against a default that has nothing to do with it.
            .WithRenderDevice(new DefaultRenderDevice
            {
                DeviceWidth = (int) options.ContentWidth,
                DeviceHeight = (int) options.ContentHeight,
                ViewPortWidth = (int) options.ContentWidth,
                ViewPortHeight = (int) options.ContentHeight,
                Category = DeviceCategory.Screen
            });

        // AngleSharp's default stylesheet is the HTML 4.01 one, which disagrees with what browsers
        // implement on most block elements. The provider is per-configuration rather than a shared
        // singleton, so appending here corrects this document's cascade without accumulating rules
        // across conversions.
        foreach (var provider in configuration.Services.OfType<ICssDefaultStyleSheetProvider>())
        {
            provider.AppendDefault(UserAgentStyles.Corrections);
        }

        var context = BrowsingContext.New(configuration);

        // Synchronous by design. Parsing is CPU-bound here — no external resource is fetched,
        // because fonts come from the FontSet rather than from the document — so an async public
        // API would offer a caller nothing to await.
        return context
            .OpenAsync(request => request
                .Content(html)
                .Address(options.BaseUrl ?? "http://localhost/"))
            .GetAwaiter()
            .GetResult();
    }

    static FontSet RequireFonts(HtmlOptions options)
    {
        if (options.Fonts is {Fallback: not null} fonts)
        {
            return fonts;
        }

        throw new InvalidOperationException(
            "HtmlOptions.Fonts must hold at least one face. Krilla has no font database, so the " +
            "fonts a document may use are supplied by the caller rather than discovered.");
    }
}

/// <summary>
/// A laid-out document, and the resources its boxes point at.
/// </summary>
/// <remarks>
/// The two travel together because an image is decoded on first paint rather than during layout,
/// so the box tree alone is not self-contained — dropping the context before painting would
/// dispose images the tree still refers to.
/// </remarks>
sealed class LayoutResult(LayoutBox root, DocumentContext context) :
    IDisposable
{
    /// <summary>The root box.</summary>
    public LayoutBox Root { get; } = root;

    /// <inheritdoc />
    public void Dispose() =>
        context.Dispose();
}
