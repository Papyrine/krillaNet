/// <summary>
/// A table's rows and cells, resolved onto a column-indexed grid.
/// </summary>
/// <remarks>
/// <para>
/// The DOM gives a table as rows of cells, which is not enough to lay one out: a cell's column is
/// not its position among its siblings, because a <c>rowspan</c> in an earlier row occupies a slot
/// in this one. Working out which column each cell actually lands in is the whole job here, and it
/// has to happen before any width can be decided.
/// </para>
/// <para>
/// Row order is render order, not document order. A <c>thead</c> comes first and a <c>tfoot</c>
/// last however the source arranged them — which is the point of the elements, since it lets a
/// long table's markup put the footer where it is convenient.
/// </para>
/// </remarks>
sealed class TableGrid
{
    /// <summary>The caption, or null when the table has none.</summary>
    public LayoutBox? Caption { get; private init; }

    /// <summary>Every row, in render order.</summary>
    public required List<TableRow> Rows { get; init; }

    /// <summary>Every cell, with its resolved position.</summary>
    public required List<TableCell> Cells { get; init; }

    /// <summary>Row groups in render order, each paired with the rows it holds.</summary>
    public required List<LayoutBox> Groups { get; init; }

    /// <summary>How many columns the grid has.</summary>
    public required int ColumnCount { get; init; }

    /// <summary>
    /// Resolves <paramref name="table"/>'s children into a grid.
    /// </summary>
    public static TableGrid Build(LayoutBox table)
    {
        var caption = default(LayoutBox);
        var headers = new List<LayoutBox>();
        var bodies = new List<LayoutBox>();
        var footers = new List<LayoutBox>();

        // Rows sitting directly in the table, with no group around them. The HTML parser inserts a
        // tbody so this is only reachable through `display: table-row` in a stylesheet, but the
        // rows still have to land somewhere in render order.
        var loose = new List<LayoutBox>();

        foreach (var child in table.Children)
        {
            switch (child.Style.Display)
            {
                case DisplayKind.TableCaption when caption is null:
                    caption = child;
                    break;
                case DisplayKind.TableHeaderGroup:
                    headers.Add(child);
                    break;
                case DisplayKind.TableFooterGroup:
                    footers.Add(child);
                    break;
                case DisplayKind.TableRowGroup:
                    bodies.Add(child);
                    break;
                case DisplayKind.TableRow:
                    loose.Add(child);
                    break;
                // A column definition contributes no box and no content. Its `width` is not read
                // yet, which is the documented gap; dropping it here is what stops an empty
                // <colgroup> from laying out as a block in the middle of the table.
                case DisplayKind.TableColumn:
                    break;
            }
        }

        var groups = new List<LayoutBox>();
        var rows = new List<TableRow>();

        foreach (var group in headers.Concat(bodies).Concat(footers))
        {
            groups.Add(group);

            foreach (var row in group.Children.Where(_ => _.Style.Display == DisplayKind.TableRow))
            {
                rows.Add(new()
                {
                    Box = row,
                    Group = group
                });
            }
        }

        foreach (var row in loose)
        {
            rows.Add(new()
            {
                Box = row
            });
        }

        var (cells, columns) = Place(rows);

        return new()
        {
            Caption = caption,
            Rows = rows,
            Cells = cells,
            Groups = groups,
            ColumnCount = columns
        };
    }

    /// <summary>
    /// Walks the rows assigning each cell a column, stepping over slots earlier spans have taken.
    /// </summary>
    /// <remarks>
    /// The occupancy set is what makes this more than counting siblings. A cell with
    /// <c>rowspan="2"</c> in the first row owns a slot in the second, so the second row's first
    /// cell belongs one column further right — and getting that wrong shears every row below a
    /// span sideways.
    /// </remarks>
    static (List<TableCell> Cells, int Columns) Place(List<TableRow> rows)
    {
        var cells = new List<TableCell>();
        var taken = new HashSet<(int Row, int Column)>();
        var columns = 0;

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var column = 0;

            foreach (var box in rows[rowIndex].Box.Children)
            {
                if (box.Style.Display != DisplayKind.TableCell)
                {
                    continue;
                }

                while (taken.Contains((rowIndex, column)))
                {
                    column++;
                }

                var columnSpan = Math.Max(1, Span(box, "colspan"));

                // A rowspan is clamped to the rows that actually exist. HTML's `rowspan="0"` means
                // "to the end of the group", and the end of the table is the closest this gets
                // without a group-aware pass.
                var declaredRowSpan = Span(box, "rowspan");
                var rowSpan = declaredRowSpan == 0
                    ? rows.Count - rowIndex
                    : Math.Min(declaredRowSpan, rows.Count - rowIndex);

                cells.Add(new(box, rowIndex, column, Math.Max(1, rowSpan), columnSpan));

                for (var r = rowIndex; r < rowIndex + rowSpan; r++)
                {
                    for (var c = column; c < column + columnSpan; c++)
                    {
                        taken.Add((r, c));
                    }
                }

                column += columnSpan;
                columns = Math.Max(columns, column);
            }
        }

        return (cells, columns);
    }

    /// <summary>
    /// A <c>colspan</c> or <c>rowspan</c> attribute.
    /// </summary>
    /// <remarks>
    /// Capped, and the cap is not paranoia: <c>colspan</c> accepts up to 1000 and a grid is
    /// allocated per column, so a hostile or mistyped document would otherwise decide how much
    /// memory the converter uses.
    /// </remarks>
    static int Span(LayoutBox box, string attribute)
    {
        if (box.Element?.GetAttribute(attribute) is not {Length: > 0} value ||
            !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var span))
        {
            return 1;
        }

        return Math.Clamp(span, 0, 1000);
    }
}
