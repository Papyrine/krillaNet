namespace Krilla.Html.Styling;

/// <summary>
/// The document-wide state style resolution needs: the matched stylesheets, and the root font size
/// that <c>rem</c> resolves against.
/// </summary>
/// <remarks>
/// Exists because reading the cascaded style needs an <see cref="IStyleCollection"/>, and building
/// one costs a pass over every stylesheet in the document. Once per conversion rather than once per
/// element.
/// </remarks>
sealed class StyleContext
{
    readonly IStyleCollection styles;

    StyleContext(IStyleCollection styles, float rootFontSize)
    {
        this.styles = styles;
        RootFontSize = rootFontSize;
    }

    /// <summary>The root element's font size in CSS pixels.</summary>
    public float RootFontSize { get; }

    /// <summary>
    /// Builds a context for <paramref name="document"/>.
    /// </summary>
    /// <remarks>
    /// The render device only decides media queries here. Percentages are deliberately not its
    /// business: the cascaded style leaves them unresolved, which is the whole reason for reading
    /// that rather than the computed style.
    /// </remarks>
    public static StyleContext For(IDocument document, HtmlOptions options)
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

        return new(window.GetStyleCollection(device), options.RootFontSize);
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
}
