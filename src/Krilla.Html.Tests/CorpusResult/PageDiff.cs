/// <summary>
/// One page's pixel comparison against the browser reference.
/// </summary>
/// <param name="Page">One-based page number.</param>
/// <param name="AbsoluteError">
/// Fraction of pixels that differ at all; 0 is identical.
/// </param>
/// <param name="Ssim">
/// Structural similarity; 1 is identical, null when the page sizes differ.
/// </param>
/// <param name="ReferenceFile">The reference render this was compared against.</param>
/// <param name="RenderedFile">Our render.</param>
public record PageDiff(
    int Page,
    double AbsoluteError,
    double? Ssim,
    string ReferenceFile,
    string RenderedFile);