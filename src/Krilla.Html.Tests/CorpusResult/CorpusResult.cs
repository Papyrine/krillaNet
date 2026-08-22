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
    /// The link annotations in the produced PDF, read back out of it.
    /// </summary>
    /// <remarks>
    /// Recorded because neither of the corpus's other measurements can see a link. A link
    /// annotation carries no appearance stream, so it paints nothing and the pixel comparison is
    /// blind to it; it is not an element box, so the geometry comparison is too. Reading the
    /// annotations back is the only way the corpus can tell that an anchor produced anything at
    /// all — and, since layout is unaffected by links, the box and pixel numbers staying at zero
    /// alongside these is itself the check that adding them disturbed nothing.
    /// </remarks>
    public List<PdfLinkRecord>? Links { get; set; }

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