/// <summary>The anchor a run of inline content sits inside.</summary>
/// <param name="Href">The <c>href</c> the annotation points at.</param>
/// <param name="Selector">
/// The path of the <c>&lt;a&gt;</c> itself, which is NOT the selector the run carries: a run takes
/// the innermost inline element's, so an anchor holding a <c>&lt;b&gt;</c> is not named by anything
/// on the run. The tag tree needs it, because a PDF <c>Link</c> element holds the annotation and
/// the text together and both have to arrive at the same node.
/// </param>
readonly record struct AnchorLink(string Href, string Selector);
