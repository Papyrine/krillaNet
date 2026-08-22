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
}