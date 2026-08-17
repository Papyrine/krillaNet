/// <summary>
/// Everything one scenario records, snapshotted through Verify.
/// </summary>
/// <remarks>
/// None of it is asserted. The snapshot is the assertion: a change in any number shows up as a
/// diff that has to be looked at and accepted, which is what makes a fidelity improvement and a
/// fidelity regression equally visible. A threshold would only tell you when something crossed it.
/// </remarks>
public class CorpusResult
{
    /// <summary>Pages the browser produced.</summary>
    public int ReferencePageCount { get; set; }

    /// <summary>Pages we produced.</summary>
    public int ResultingPageCount { get; set; }

    /// <summary>How far our element geometry is from the browser's.</summary>
    public BoxComparisonResult? Boxes { get; set; }

    /// <summary>
    /// Per-page pixel comparison, or null when the page counts differ.
    /// </summary>
    /// <remarks>
    /// Null rather than a partial comparison: with different page counts, page N on one side is
    /// not page N on the other, so every number would be comparing unrelated pages. The null is
    /// itself the signal that pagination diverged — and it is a blind spot, which is what
    /// <see cref="BaselineHealthTests"/> exists to cover.
    /// </remarks>
    public List<PageDiff>? PageDiffs { get; set; }
}

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

[JsonSerializable(typeof(CorpusResult))]
public partial class CorpusResultContext : JsonSerializerContext;
