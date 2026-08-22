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