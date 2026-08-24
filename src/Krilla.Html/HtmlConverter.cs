namespace Krilla.Html;

/// <summary>
/// Converts HTML to PDF. The entry point to the library.
/// </summary>
/// <remarks>
/// <para>
/// Three stages, each independently inspectable: AngleSharp parses the markup and runs the CSS
/// cascade, <see cref="Layout"/> turns the styled tree into positioned boxes, and
/// <see cref="PdfPainter"/> draws those boxes through krilla. The middle stage is the one
/// with all the specification in it, and <see cref="LayoutDocument"/> exposes it directly so it
/// can be measured without going near a PDF.
/// </para>
/// <para>
/// Media queries resolve against PRINT, and a document's <c>@page</c> rules decide the paper unless
/// <see cref="HtmlOptions.HonourPageRules"/> says otherwise — both because a PDF is print, and the
/// second because a document declaring A4 means it.
/// </para>
/// <para>
/// What lays out as a plain block instead of in a mode of its own: flexbox, grid and multi-column.
/// <see cref="HtmlOptions.OnDiagnostic"/> reports each of them, along with everything else
/// recognised and not rendered the way a browser would.
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
        // Before anything measures a page: the geometry decides the containing block every
        // percentage resolves against and every viewport unit, so a document declaring its own
        // paper has to be read first or the whole tree is laid out against the wrong rectangle.
        options = Paged(document, options);

        // The layout holds the decoded images, so it has to outlive painting — they are decoded
        // on first draw, not during layout.
        using var layout = LayoutDocument(document, options);
        var root = layout.Root;

        using var pdf = new KrillaDocument();

        if (Metadata(document, options) is {} metadata)
        {
            pdf.SetMetadata(metadata);
        }

        var scale = 1 / HtmlOptions.PixelsPerPoint;
        var content = new Rect(
            options.MarginLeft,
            options.MarginTop,
            options.ContentWidth,
            options.ContentHeight);

        var tops = Paginator.PageTops(root, options.ContentHeight, options.HonourOrphansAndWidows);

        // After pagination, because a fragment names an element while a PDF internal link names a
        // page and a point on it — and which page an element landed on is what pagination decides.
        var links = LinkTargets.Build(root, tops, content, scale);

        // Both of these are addressed by page and point, so both wait for pagination too.
        if (DocumentOutline.Build(root, tops, content, scale, options.OutlineDepth) is {Count: > 0} outline)
        {
            pdf.SetOutline(outline);
        }

        if (options.NamedDestinations)
        {
            foreach (var (name, page, target) in links.All())
            {
                pdf.RegisterDestination(name, page, target);
            }
        }

        for (var index = 0; index < tops.Count; index++)
        {
            using var page = pdf.StartPage(
                options.PageWidth * scale,
                options.PageHeight * scale);

            // Each page runs to where the next one begins, which is a line boundary rather than
            // the bottom of the paper. The last page runs to infinity so nothing is trimmed off
            // the end of the document.
            var end = index + 1 < tops.Count ? tops[index + 1] : float.PositiveInfinity;

            PdfPainter.Paint(
                page.Surface,
                root,
                tops[index],
                end,
                content,
                new(options.PageWidth * scale, options.PageHeight * scale),
                scale,
                links);
        }

        return pdf.Finish();
    }

    /// <summary>
    /// The metadata to write, or null when there is none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The document's own <c>&lt;title&gt;</c> and <c>lang</c> fill what the caller left unset rather
    /// than overriding it: a caller naming a title means it, and a document that names one should not
    /// be published with no title just because nobody passed options. The language matters more than
    /// it looks — it is what lets a reader pronounce the text, and a PDF without it is inaccessible
    /// in a way nothing on the page shows.
    /// </para>
    /// <para>
    /// Null when there is nothing at all, so a document with neither produces the same bytes it
    /// produced before this existed. Worth the branch: writing an empty metadata dictionary would
    /// change every PDF this library has ever emitted, for nothing.
    /// </para>
    /// <para>
    /// A COPY, because the caller's object is often reused across conversions and one document's
    /// title leaking into the next would be a memorable bug — the same reason
    /// <see cref="HtmlOptions.WithPage"/> copies.
    /// </para>
    /// </remarks>
    static DocumentMetadata? Metadata(IDocument document, HtmlOptions options)
    {
        var title = Text(document.Title);
        var language = Text(document.DocumentElement?.GetAttribute("lang"));

        if (options.Metadata is not {} given)
        {
            return title is null && language is null
                ? null
                : new()
                {
                    Title = title,
                    Language = language
                };
        }

        return new()
        {
            Title = given.Title ?? title,
            Language = given.Language ?? language,
            Description = given.Description,
            Creator = given.Creator,
            Producer = given.Producer,
            DocumentId = given.DocumentId,
            Authors = given.Authors,
            Keywords = given.Keywords,
            CreationDate = given.CreationDate,
            TextDirection = given.TextDirection,
            PageLayout = given.PageLayout
        };
    }

    /// <summary>
    /// A trimmed string, or null when there was nothing in it.
    /// </summary>
    /// <remarks>
    /// AngleSharp gives an empty string rather than null for an absent <c>&lt;title&gt;</c> and for
    /// an absent attribute, and an empty title in a PDF is worse than none: a reader shows the file
    /// name when the title is absent and a blank when it is present and empty.
    /// </remarks>
    static string? Text(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Folds a document's <c>@page</c> rules into the options, or returns them unchanged.
    /// </summary>
    /// <remarks>
    /// An orientation keyword is applied to whatever size resulted rather than to the one the rule
    /// named, so <c>@page { size: landscape }</c> turns the caller's paper and
    /// <c>size: A4 landscape</c> turns A4 — which is the same rule stated once.
    /// </remarks>
    internal static HtmlOptions Paged(IDocument document, HtmlOptions options)
    {
        if (!options.HonourPageRules)
        {
            return options;
        }

        var rules = PageRules.For(document, options.RootFontSize);

        foreach (var (property, value, reason) in rules.Unsupported)
        {
            Diagnostic.Rule(options.OnDiagnostic, "@page", property, value, reason);
        }

        if (!rules.Any)
        {
            return options;
        }

        var (width, height) = rules.Size ?? (options.PageWidth, options.PageHeight);

        if (rules.Landscape is {} landscape && landscape != width > height)
        {
            (width, height) = (height, width);
        }

        return options.WithPage(
            width,
            height,
            rules.MarginTop ?? options.MarginTop,
            rules.MarginRight ?? options.MarginRight,
            rules.MarginBottom ?? options.MarginBottom,
            rules.MarginLeft ?? options.MarginLeft);
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

        // After flow, never during it. An absolute box is positioned against an ancestor that is
        // sized by flowing the very children that may declare it, so the only order without a
        // circle is to finish flow and then descend. The initial containing block is the page
        // content area, which is what paged media has instead of a viewport.
        AbsoluteLayout.Place(
            root,
            new(0, 0, options.ContentWidth, options.ContentHeight),
            fonts);

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