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
    /// <remarks>
    /// Asynchronous because parsing is: AngleSharp's document loader is async throughout, and this
    /// awaits it rather than blocking on it. Everything after the parse — layout and painting — is
    /// CPU-bound and runs on the awaiting thread, so the returned task completes with the PDF
    /// rather than yielding again.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// <see cref="HtmlOptions.Fonts"/> was not set, or holds no faces.
    /// </exception>
    public static async Task<byte[]> ConvertAsync(
        string html,
        HtmlOptions options,
        Cancel cancel = default)
    {
        var document = await ParseAsync(html, options, cancel);

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
        options = Paged(document, options, out var rules);

        // The layout holds the decoded images, so it has to outlive painting — they are decoded
        // on first draw, not during layout. The context comes with it, because a margin box's
        // `content: url()` resolves through the same store the document's images do.
        using var layout = LayoutDocument(document, options);
        var root = layout.Root;
        var fonts = RequireFonts(options);

        // Tagging has to be asked for at construction: krilla refuses a tag tree on a document
        // that was not built to carry one.
        var tags = options.Tagged ? new DocumentTags() : null;

        using var pdf = tags is null
            ? new KrillaDocument()
            : new KrillaDocument(new() {EnableTagging = true});

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

        var pages = Paginator.Paginate(root, options.ContentHeight, options.HonourOrphansAndWidows);

        // After pagination, because a fragment names an element while a PDF internal link names a
        // page and a point on it — and which page an element landed on is what pagination decides.
        var links = LinkTargets.Build(root, pages, content, scale);

        // After pagination, because a named string's value is a function of where the boundaries
        // fell: the same heading sets it on every page it precedes, and which of those pages is
        // asking decides the answer.
        var strings = RunningStrings.Build(root);

        // And which named page each sheet belongs to, for the same reason: `page: cover` names an
        // element, and which sheets that element occupies is a pagination result.
        var names = PageNames.Build(root);

        // Both of these are addressed by page and point, so both wait for pagination too.
        if (DocumentOutline.Build(root, pages, content, scale, options.OutlineDepth) is {Count: > 0} outline)
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

        for (var index = 0; index < pages.Count; index++)
        {
            using var page = pdf.StartPage(
                options.PageWidth * scale,
                options.PageHeight * scale);

            // Each page runs to where the next one begins, which is a line boundary rather than
            // the bottom of the paper. The last page runs to infinity so nothing is trimmed off
            // the end of the document.
            var end = index + 1 < pages.Count ? pages[index + 1].Top : float.PositiveInfinity;

            // A page a forced break left empty, which `@page :blank` selects. Its slice runs
            // from its own top to the same place, so it holds nothing but the canvas — and a
            // stylesheet that suppresses the running header on such a page is the reason the
            // selector exists.
            var blank = end <= pages[index].Top;

            PdfPainter.Paint(
                page.Surface,
                root,
                pages[index],
                end,
                content,
                new(options.PageWidth * scale, options.PageHeight * scale),
                scale,
                links,
                PageMargins.Build(
                    rules,
                    options,
                    document,
                    layout.Context,
                    fonts,
                    root.Style.FontFamilies,
                    index + 1,
                    pages.Count,
                    blank,
                    new(strings, pages[index].Top, end),
                    names.Value(pages[index].Top)),
                tags);
        }

        // After every page has closed, which is when the spans painted on them resolve — and the
        // tree is built from the DOM rather than from what was painted, because the painter emits
        // content in Appendix E's phases and a reader follows document order.
        using var tree = tags?.Build(document, Text(document.DocumentElement.GetAttribute("lang")));

        if (tree is not null)
        {
            pdf.SetTagTree(tree);
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
        var language = Text(document.DocumentElement.GetAttribute("lang"));

        if (options.Metadata is not {} given)
        {
            if (title is null && language is null)
            {
                return null;
            }

            return new()
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
    static string? Text(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    /// <summary>
    /// Folds a document's <c>@page</c> rules into the options, or returns them unchanged.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An orientation keyword is applied to whatever size resulted rather than to the one the rule
    /// named, so <c>@page { size: landscape }</c> turns the caller's paper and
    /// <c>size: A4 landscape</c> turns A4 — which is the same rule stated once.
    /// </para>
    /// <para>
    /// The rules are read whatever <see cref="HtmlOptions.HonourPageRules"/> says, and only the
    /// GEOMETRY is withheld when it is false. That flag is about the paper — a caller whose page
    /// size comes from a printer or an envelope — and a running header is content rather than
    /// paper.
    /// </para>
    /// </remarks>
    internal static HtmlOptions Paged(IDocument document, HtmlOptions options, out PageRules rules)
    {
        rules = PageRules.For(document, options.RootFontSize);

        foreach (var (property, value, reason) in rules.Unsupported)
        {
            Diagnostic.Rule(options.OnDiagnostic, "@page", property, value, reason);
        }

        if (!options.HonourPageRules || !rules.Any)
        {
            return options;
        }

        var (width, height) = rules.Size ?? (options.PageWidth, options.PageHeight);

        if (rules.Landscape is {} landscape &&
            landscape != width > height)
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

        // Before the tree, because it is a property of the DOCUMENT rather than of any element and
        // there is nothing in the walk below to attach it to.
        context.ReportFontFaces();

        var root = BoxBuilder.Build(element, initial, context);

        // The root box's containing block is the page's content area, which is the print
        // equivalent of the browser's initial containing block. Laid out at the root's own top
        // margin — margins never collapse out of the root, and nothing above it would apply them
        // if they did. Everything stays document-relative from there; page margins are applied
        // once, when painting.
        var top = root.Style.MarginTop.Resolve(options.ContentWidth);

        // The page's content HEIGHT goes with its width, which is what makes `html { height: 50% }`
        // half a sheet rather than nothing at all. In paged media the initial containing block is
        // the page area, so a percentage height at the root has a definite basis where in a
        // scrolling viewport it would have none.
        BlockLayout.Layout(
            root,
            0,
            top,
            options.ContentWidth,
            fonts,
            containingHeight: options.ContentHeight);

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
    internal static Task<IDocument> ParseAsync(
        string html,
        HtmlOptions options,
        Cancel cancel = default)
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

        // Awaited rather than blocked on. Nothing is fetched over the network here — fonts come
        // from the FontSet and images go through ImageStore's policy — so the task usually
        // completes synchronously, and awaiting it is what keeps that an implementation detail
        // rather than a deadlock waiting for a loader that does reach outside.
        return context
            .OpenAsync(
                _ => _
                    .Content(html)
                    .Address(options.BaseUrl ?? "http://localhost/"),
                cancel);
    }

    static FontSet RequireFonts(HtmlOptions options)
    {
        if (options.Fonts is {Fallback: not null} fonts)
        {
            return fonts;
        }

        throw new InvalidOperationException(
            "HtmlOptions.Fonts must hold at least one face. Krilla has no font database, so the fonts a document may use are supplied by the caller rather than discovered.");
    }
}