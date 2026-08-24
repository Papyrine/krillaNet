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
            Category = DeviceCategory.Screen
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
            options.OnDiagnostic);
    }

    /// <summary>
    /// The declarations that matched <paramref name="element"/>, with relative units and
    /// percentages left as written.
    /// </summary>
    /// <remarks>
    /// No inherited values: a property no rule set comes back empty rather than carrying the
    /// parent's. That suits, because inheritance is applied in
    /// <see cref="StyleResolver.Resolve"/> against a parent whose values are already resolved —
    /// doing it here would mean inheriting a string and resolving it twice.
    /// </remarks>
    public ICssStyleDeclaration Cascade(IElement element) =>
        styles.ComputeCascadedStyle(element, null!);

    /// <inheritdoc />
    public void Dispose() =>
        Images.Dispose();
}
