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

    /// <summary>
    /// The document-wide state the tree was built against.
    /// </summary>
    /// <remarks>
    /// Reached after layout by the page margin boxes, whose `content: url()` resolves through the
    /// same image store the document's own images do — and which are built per page, long after
    /// the walk that made this.
    /// </remarks>
    public DocumentContext Context { get; } = context;

    /// <inheritdoc />
    public void Dispose() =>
        Context.Dispose();
}