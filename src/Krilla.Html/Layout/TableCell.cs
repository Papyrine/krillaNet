/// <summary>
/// Where one cell sits in the grid, after spans have been resolved.
/// </summary>
/// <param name="Box">The cell's box.</param>
/// <param name="Row">Index of the row the cell starts in, in render order.</param>
/// <param name="Column">Index of the column the cell starts in.</param>
/// <param name="RowSpan">How many rows it covers.</param>
/// <param name="ColumnSpan">How many columns it covers.</param>
sealed record TableCell(LayoutBox Box, int Row, int Column, int RowSpan, int ColumnSpan);