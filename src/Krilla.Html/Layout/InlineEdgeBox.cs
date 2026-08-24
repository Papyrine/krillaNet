/// <summary>
/// One end of an inline element on one line: the strip its padding and border occupy.
/// </summary>
/// <param name="Style">The element's style, for the colours and widths.</param>
/// <param name="Face">Its resolved face, which sizes the strip vertically.</param>
/// <param name="Bounds">The strip's border box.</param>
/// <param name="Kind">Which end this is, and so which side border is drawn.</param>
/// <param name="Selector">
/// The element's path. Carried for the box dump, where an edge is what gives a padded inline its
/// real extent — a browser's rectangle for one covers the padding and border, and the text runs
/// alone reach neither.
/// </param>
/// <param name="Baseline">
/// The baseline of the line this edge sits on, so an enclosing element's own extent can be
/// recovered from it.
/// </param>
/// <param name="Ancestors">The inline elements enclosing this one, outermost first, or null.</param>
readonly record struct InlineEdgeBox(
    ComputedStyle Style,
    FontFace Face,
    Rect Bounds,
    InlineEdgeKind Kind,
    string? Selector,
    float Baseline,
    IReadOnlyList<InlineBackdrop>? Ancestors);