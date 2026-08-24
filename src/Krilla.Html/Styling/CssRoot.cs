namespace Krilla.Html.Styling;

/// <summary>
/// The document-wide context a relative length resolves against: the root font size and the
/// viewport.
/// </summary>
/// <remarks>
/// <para>
/// Bundled rather than passed as two or four separate floats because every parse in
/// <see cref="StyleResolver"/> threads it through, and each unit added would otherwise be a
/// signature change at seventy call sites. The element's OWN font size stays a separate parameter,
/// since it changes per element while everything here is fixed for the document.
/// </para>
/// <para>
/// In paged media the viewport is the page's content box rather than a window, so <c>100vh</c> is
/// one page tall and a box sized that way fills the sheet. That is what a browser printing to PDF
/// does, and it is why the corpus can measure viewport units at all — its pages have no margins,
/// so the page content box and the browser's screen viewport are the same rectangle.
/// </para>
/// </remarks>
readonly record struct CssRoot(float FontSize, float ViewportWidth, float ViewportHeight)
{
    /// <summary>A context with no viewport, for the parses that cannot see one.</summary>
    /// <remarks>
    /// Viewport units resolve to zero under it rather than being rejected. The alternative is a
    /// nullable viewport that every unit has to test, to distinguish two cases that no caller
    /// treats differently.
    /// </remarks>
    public static CssRoot Default => new(16, 0, 0);

    /// <summary>The smaller viewport axis, which <c>vmin</c> is a percentage of.</summary>
    public float ViewportMin => MathF.Min(ViewportWidth, ViewportHeight);

    /// <summary>The larger viewport axis, which <c>vmax</c> is a percentage of.</summary>
    public float ViewportMax => MathF.Max(ViewportWidth, ViewportHeight);
}
