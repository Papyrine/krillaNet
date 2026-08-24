/// <summary>
/// One <c>&lt;col&gt;</c> or <c>&lt;colgroup&gt;</c>, and where it ended up.
/// </summary>
/// <param name="Selector">The element's path.</param>
/// <param name="First">The first column it covers.</param>
/// <param name="Span">How many columns it covers.</param>
/// <remarks>
/// <see cref="Bounds"/> is filled by table layout, since a column definition has no geometry of its
/// own until the columns are sized.
/// </remarks>
sealed record ColumnBox(string Selector, int First, int Span)
{
    /// <summary>The rectangle a browser reports for it: its columns' extent by the row area.</summary>
    public Rect Bounds { get; set; }
}