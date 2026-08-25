/// <summary>One row, and the group it belongs to.</summary>
sealed class TableRow
{
    /// <summary>The row's box.</summary>
    public required LayoutBox Box { get; init; }

    /// <summary>The row group containing it, or null when the row sits directly in the table.</summary>
    public LayoutBox? Group { get; init; }

    /// <summary>Top edge, relative to the table's content box.</summary>
    public float Y { get; set; }

    /// <summary>Height, once the cells in it have been measured.</summary>
    public float Height { get; set; }

    /// <summary>
    /// Where the row's baseline sits below its top edge, once its cells have been measured.
    /// </summary>
    /// <remarks>
    /// The furthest any <c>vertical-align: baseline</c> cell in the row carries its own first
    /// baseline below its border-box top. Zero when no cell in the row asks for it, which is the
    /// usual case — the user-agent sheet makes a cell <c>middle</c>.
    /// </remarks>
    public float Baseline { get; set; }
}