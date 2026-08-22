/// <summary>
/// One link annotation, as the produced PDF actually contains it.
/// </summary>
/// <param name="Page">One-based page the annotation is on.</param>
/// <param name="Target">
/// The URI for an external link, or <c>page N</c> for an internal one.
/// </param>
/// <param name="X">Left edge, in CSS pixels from the top-left of the page.</param>
/// <param name="Y">Top edge, in CSS pixels from the top-left of the page.</param>
/// <param name="Width">Width in CSS pixels.</param>
/// <param name="Height">Height in CSS pixels.</param>
/// <remarks>
/// Converted out of PDF space into the corpus's own units — CSS pixels, Y increasing downward —
/// so these numbers can be read against the box geometry directly. PDF space is points with the
/// origin at the bottom-left, which would make every review an exercise in mental arithmetic.
/// </remarks>
public record PdfLinkRecord(int Page, string Target, double X, double Y, double Width, double Height);