/// <summary>One flex line: the items that share it, and the cross-axis band they occupy.</summary>
/// <remarks>
/// A line exists even in a <c>nowrap</c> container, where there is exactly one of them and it may
/// overflow. Keeping the single-line case as a list of one is what stops the algorithm growing a
/// second path — every rule below the line collection is written per line, and the wrapping
/// container is the same code run more than once.
/// </remarks>
sealed class FlexLine
{
    public required List<FlexItem> Items { get; init; }

    /// <summary>The cross size the line ended up with.</summary>
    public float Cross { get; set; }

    /// <summary>Where the line's cross-start edge sits, relative to the content box.</summary>
    public float CrossPosition { get; set; }

    /// <summary>
    /// The largest distance from any baseline-aligned item's outer cross-start edge down to its
    /// baseline, which is where the line puts the shared baseline.
    /// </summary>
    public float Baseline { get; set; }
}
