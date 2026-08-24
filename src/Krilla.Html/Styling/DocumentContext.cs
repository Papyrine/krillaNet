namespace Krilla.Html.Styling;

/// <summary>
/// The state that building a box tree needs once per document rather than once per element: the
/// matched stylesheets, the root font size that <c>rem</c> resolves against, and the resolved
/// images.
/// </summary>
/// <remarks>
/// Exists because reading the cascaded style needs an <see cref="IStyleCollection"/>, and building
/// one costs a pass over every stylesheet in the document.
/// </remarks>
sealed class DocumentContext :
    IDisposable
{
    readonly IStyleCollection styles;

    DocumentContext(
        IStyleCollection styles,
        CssRoot root,
        ImageStore images,
        Action<HtmlDiagnostic>? onDiagnostic)
    {
        this.styles = styles;
        Root = root;
        Images = images;
        OnDiagnostic = onDiagnostic;
    }

    /// <summary>The root element's font size in CSS pixels.</summary>
    public float RootFontSize => Root.FontSize;

    /// <summary>What <c>rem</c> and the viewport units resolve against.</summary>
    public CssRoot Root { get; }

    /// <summary>
    /// The CSS counters in scope, mutated as the box tree is built.
    /// </summary>
    /// <remarks>
    /// Document-wide state carried here rather than threaded through the builder's recursion,
    /// alongside the images and the cascade — it belongs to the same phase and has the same
    /// lifetime.
    /// </remarks>
    public CssCounters Counters { get; } = new();

    /// <summary>
    /// The column widths collected while one table's children are being walked, and the definitions
    /// they came from.
    /// </summary>
    /// <remarks>
    /// Here rather than on the box builder because the builder is STATIC and conversions run
    /// concurrently — a static field for this raced between two scenarios in the same test run and
    /// produced a table sized by another document's columns. It reads as per-table state and is
    /// per-document state for the same reason the counters are: the walk is where it is filled, and
    /// the walk saves and restores it around each table.
    /// </remarks>
    public List<CssLength> PendingColumns { get; set; } = [];

    /// <inheritdoc cref="PendingColumns"/>
    public List<ColumnBox> PendingColumnBoxes { get; set; } = [];

    /// <summary>
    /// How deeply quotations are nested, for <c>open-quote</c> and <c>close-quote</c>.
    /// </summary>
    /// <remarks>
    /// A single depth for the document rather than one per element, which is what CSS specifies:
    /// the marks a quote draws depend on how many quotations are open anywhere above it, so nesting
    /// a quotation inside another changes its marks.
    /// </remarks>
    public int QuoteDepth { get; set; }

    /// <summary>
    /// Whether <c>orphans</c> and <c>widows</c> are honoured, from
    /// <see cref="HtmlOptions.HonourOrphansAndWidows"/>.
    /// </summary>
    /// <remarks>
    /// Carried here for the diagnostic table alone: the properties are read into every style
    /// regardless, and whether reading them means anything is a document-wide decision the
    /// per-element reporter has no other way to see.
    /// </remarks>
    public bool ConstrainRuns { get; private init; }

    /// <summary>Images resolved from <c>src</c> attributes, deduplicated across the document.</summary>
    public ImageStore Images { get; }

    /// <summary>Where unrendered constructs are reported, or null when nobody subscribed.</summary>
    public Action<HtmlDiagnostic>? OnDiagnostic { get; }

    /// <summary>
    /// Whether anything is listening, so the work of finding what to report is worth doing.
    /// </summary>
    /// <remarks>
    /// Checked before the scan rather than inside it. <see cref="UnsupportedCss"/> costs a cascade
    /// lookup per property per element, which a caller who is not subscribed should not pay for.
    /// </remarks>
    [MemberNotNullWhen(true, nameof(OnDiagnostic))]
    public bool Reports => OnDiagnostic is not null;

    /// <summary>
    /// Builds a context for <paramref name="document"/>.
    /// </summary>
    /// <remarks>
    /// The render device only decides media queries here. Percentages are deliberately not its
    /// business: the cascaded style leaves them unresolved, which is the whole reason for reading
    /// that rather than the computed style.
    /// </remarks>
    public static DocumentContext For(IDocument document, HtmlOptions options)
    {
        var device = new DefaultRenderDevice
        {
            DeviceWidth = (int) options.ContentWidth,
            DeviceHeight = (int) options.ContentHeight,
            ViewPortWidth = (int) options.ContentWidth,
            ViewPortHeight = (int) options.ContentHeight,
            // A PDF is print. Media queries resolved against `Screen` mean a document's
            // `@media print` block — the one written FOR this — was excluded while its
            // `@media screen` block was applied, which is the wrong way round for every document
            // that has both. The corpus reference agrees: its page renders always came from
            // Chromium's printer, and its box geometry now does too.
            Category = DeviceCategory.Printer
        };

        var window = document.DefaultView ??
                     throw new InvalidOperationException("The document has no view to resolve styles against.");

        var images = new ImageStore(
            options.ImageResolver ?? ImageStore.DefaultResolver(options.BaseUrl),
            options.LocalImages,
            options.WebImages);

        return new(
            window.GetStyleCollection(device),
            // The viewport in paged media is the page's CONTENT box, which is what a browser
            // printing to PDF resolves `vh` and `vw` against — so `height: 100vh` fills the sheet
            // between the margins rather than the sheet itself.
            new(options.RootFontSize, options.ContentWidth, options.ContentHeight),
            images,
            options.OnDiagnostic)
        {
            ConstrainRuns = options.HonourOrphansAndWidows
        };
    }

    /// <summary>
    /// The declarations that matched <paramref name="element"/>, with relative units and
    /// percentages left as written.
    /// </summary>
    /// <remarks>
    /// No inherited values: a property no rule set comes back empty rather than carrying the
    /// parent's. That suits, because inheritance is applied in
    /// <see cref="StyleResolver.Resolve(IElement, ComputedStyle, DocumentContext)"/> against a parent whose values are already resolved —
    /// doing it here would mean inheriting a string and resolving it twice.
    /// </remarks>
    public ICssStyleDeclaration Cascade(IElement element) =>
        styles.ComputeCascadedStyle(element, null!);

    /// <summary>
    /// The cascaded style of one of <paramref name="element"/>'s pseudo-elements, or null when the
    /// document has no rule for it.
    /// </summary>
    /// <remarks>
    /// A separate route from the overload above because a pseudo-element is not an element: it
    /// has no place in the tree and AngleSharp materialises it on request. The style it comes back
    /// with is a real cascade result all the same, carrying whatever the document declared —
    /// including <c>content</c>, which is the one property that only exists here.
    /// </remarks>
    public static ICssStyleDeclaration? Cascade(IElement element, string pseudo) =>
        element.Pseudo(pseudo)?.GetCascadedStyle();

    /// <inheritdoc />
    public void Dispose() =>
        Images.Dispose();
}
