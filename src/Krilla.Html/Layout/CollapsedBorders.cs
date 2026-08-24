/// <summary>
/// One resolved grid line, ready to paint.
/// </summary>
/// <param name="Bounds">The rectangle the line fills.</param>
/// <param name="Color">Its colour.</param>
sealed record CollapsedLine(Rect Bounds, Color Color);

/// <summary>
/// The collapsing border model: one line per grid edge, shared by the boxes either side of it.
/// </summary>
/// <remarks>
/// <para>
/// Under <c>border-collapse: collapse</c> a table has no border-spacing and adjacent cells do not
/// each draw an edge. Every boundary in the grid carries ONE line, whose width and colour are won
/// by one of the boxes touching it, and that line is centred on the boundary — so each box's used
/// border on an edge is HALF the line there, and the table's own box reaches half a line beyond
/// its outermost cells.
/// </para>
/// <para>
/// That halving is the whole trick, and it is why this runs before anything measures a cell. The
/// column algorithm sizes columns from cell border-box widths, so the halved widths have to be in
/// the cells' styles by the time it looks — resolving them afterwards would size the table with
/// one set of borders and paint it with another.
/// </para>
/// <para>
/// Painting is the other half. Two cells each drawing their own half would seam down the middle of
/// every line at any odd width — 3px gives two 1.5px halves meeting on a half pixel, which
/// antialiases into a visible join. So the boxes paint no borders at all in this mode and the lines
/// are drawn once, from <see cref="LayoutBox.CollapsedLines"/>.
/// </para>
/// <para>
/// Every number here was measured out of Chrome: 2px uniform on 80px cells with 6px padding gives
/// 94px cells inside a 190px table; a 6px cell against a 2px neighbour gives 98px against 96px; a
/// 4px table border against 2px cells puts the cells 2px inside the table box; and 3px gives cells
/// at x=1.5. <c>table/collapse</c> keeps all four.
/// </para>
/// </remarks>
sealed class CollapsedBorders
{
    /// <summary>
    /// Which box an edge's border came from, highest priority first when widths and styles tie.
    /// </summary>
    /// <remarks>
    /// CSS 2.1 §17.6.2's origin order, minus the two this engine cannot reach: a column and a
    /// column group generate no box here, so they can carry no border to contribute.
    /// </remarks>
    enum Origin
    {
        Table,
        Group,
        Row,
        Cell
    }

    readonly record struct Edge(float Width, Color? Color, BorderStyleKind Style, Origin Origin)
    {
        public static readonly Edge None = new(0, null, BorderStyleKind.Solid, Origin.Table);

        /// <summary>
        /// The winner of a conflict between two candidates for one edge.
        /// </summary>
        /// <remarks>
        /// Widest first, then style, then origin — the specification's order. A tie on all three
        /// means the two are indistinguishable and either answer is the same answer.
        /// </remarks>
        public Edge Against(Edge other)
        {
            // `hidden` wins outright, ahead of width — CSS's own rule and the whole point of the
            // value: it is how a table suppresses one internal rule without touching the cells
            // around it. Checked first because a hidden border carries no width to compare, so
            // every test below would hand the edge to its neighbour.
            if (Style == BorderStyleKind.Hidden || other.Style == BorderStyleKind.Hidden)
            {
                return Style == BorderStyleKind.Hidden ? this : other;
            }

            if (Width != other.Width)
            {
                return Width > other.Width ? this : other;
            }

            var mine = Rank(Style);
            var theirs = Rank(other.Style);

            if (mine != theirs)
            {
                return mine > theirs ? this : other;
            }

            return Origin >= other.Origin ? this : other;
        }

        /// <summary>
        /// Style precedence, higher winning. <c>double</c> over <c>solid</c> over the broken
        /// styles is the specification's order; the shaded styles are absent because
        /// <see cref="StyleResolver"/> never produces them.
        /// </summary>
        static int Rank(BorderStyleKind style) =>
            style switch
            {
                BorderStyleKind.Double => 4,
                BorderStyleKind.Solid => 3,
                BorderStyleKind.Dashed => 2,
                _ => 1
            };

    }

    readonly Edge[,] vertical;
    readonly Edge[,] horizontal;
    readonly int rows;
    readonly int columns;

    CollapsedBorders(Edge[,] vertical, Edge[,] horizontal, int rows, int columns)
    {
        this.vertical = vertical;
        this.horizontal = horizontal;
        this.rows = rows;
        this.columns = columns;
    }

    /// <summary>
    /// Resolves every grid line and rewrites the boxes' borders to their halves.
    /// </summary>
    /// <remarks>
    /// The rewrite is what makes the rest of table layout work unchanged: a cell whose style says
    /// it has a 1px left border behaves exactly like any other cell with a 1px left border, so the
    /// column algorithm, the row heights and the cell placement need to know nothing about this
    /// model. Colours are cleared at the same time, so the boxes paint no borders and the lines are
    /// drawn once instead.
    /// </remarks>
    public static CollapsedBorders? Resolve(TableGrid grid, LayoutBox table)
    {
        if (table.Style.BorderCollapse != BorderCollapseKind.Collapse ||
            grid.Rows.Count == 0 ||
            grid.ColumnCount == 0)
        {
            return null;
        }

        var rows = grid.Rows.Count;
        var columns = grid.ColumnCount;

        var occupancy = Occupancy(grid, rows, columns);
        var vertical = new Edge[columns + 1, rows];
        var horizontal = new Edge[rows + 1, columns];

        for (var row = 0; row < rows; row++)
        {
            for (var line = 0; line <= columns; line++)
            {
                vertical[line, row] = Vertical(grid, occupancy, table, row, line, columns);
            }
        }

        for (var line = 0; line <= rows; line++)
        {
            for (var column = 0; column < columns; column++)
            {
                horizontal[line, column] = Horizontal(grid, occupancy, table, line, column, rows);
            }
        }

        var borders = new CollapsedBorders(vertical, horizontal, rows, columns);
        borders.Apply(grid, table);
        return borders;
    }

    /// <summary>Which cell owns each slot, so an edge can find the boxes either side of it.</summary>
    static TableCell?[,] Occupancy(TableGrid grid, int rows, int columns)
    {
        var occupancy = new TableCell?[rows, columns];

        foreach (var cell in grid.Cells)
        {
            for (var row = cell.Row; row < Math.Min(rows, cell.Row + cell.RowSpan); row++)
            {
                for (var column = cell.Column; column < Math.Min(columns, cell.Column + cell.ColumnSpan); column++)
                {
                    occupancy[row, column] = cell;
                }
            }
        }

        return occupancy;
    }

    static Edge Vertical(
        TableGrid grid,
        TableCell?[,] occupancy,
        LayoutBox table,
        int row,
        int line,
        int columns)
    {
        var winner = Edge.None;

        if (line > 0 && occupancy[row, line - 1] is {} left)
        {
            winner = winner.Against(Of(left.Box.Style, Side.Right, Origin.Cell));
        }

        if (line < columns && occupancy[row, line] is {} right)
        {
            winner = winner.Against(Of(right.Box.Style, Side.Left, Origin.Cell));
        }

        // A row's own side borders reach only the outer edges of the grid, and a group's likewise.
        if (line == 0 || line == columns)
        {
            var side = line == 0 ? Side.Left : Side.Right;

            winner = winner.Against(Of(grid.Rows[row].Box.Style, side, Origin.Row));

            if (grid.Rows[row].Group is {} group)
            {
                winner = winner.Against(Of(group.Style, side, Origin.Group));
            }

            winner = winner.Against(Of(table.Style, side, Origin.Table));
        }

        return winner;
    }

    static Edge Horizontal(
        TableGrid grid,
        TableCell?[,] occupancy,
        LayoutBox table,
        int line,
        int column,
        int rows)
    {
        var winner = Edge.None;

        if (line > 0 && occupancy[line - 1, column] is {} above)
        {
            winner = winner.Against(Of(above.Box.Style, Side.Bottom, Origin.Cell));
        }

        if (line < rows && occupancy[line, column] is {} below)
        {
            winner = winner.Against(Of(below.Box.Style, Side.Top, Origin.Cell));
        }

        // The row above contributes its bottom edge and the row below its top, whether or not the
        // line is at the table's own boundary — a row border shows between rows too.
        if (line > 0)
        {
            winner = winner.Against(Of(grid.Rows[line - 1].Box.Style, Side.Bottom, Origin.Row));
        }

        if (line < rows)
        {
            winner = winner.Against(Of(grid.Rows[line].Box.Style, Side.Top, Origin.Row));
        }

        if (line == 0 || line == rows)
        {
            var side = line == 0 ? Side.Top : Side.Bottom;
            var row = grid.Rows[line == 0 ? 0 : rows - 1];

            if (row.Group is {} group)
            {
                winner = winner.Against(Of(group.Style, side, Origin.Group));
            }

            winner = winner.Against(Of(table.Style, side, Origin.Table));
        }

        return winner;
    }

    enum Side
    {
        Top,
        Right,
        Bottom,
        Left
    }

    static Edge Of(ComputedStyle style, Side side, Origin origin) =>
        side switch
        {
            Side.Top => new(style.BorderTop, style.BorderTopColor, style.BorderTopStyle, origin),
            Side.Right => new(style.BorderRight, style.BorderRightColor, style.BorderRightStyle, origin),
            Side.Bottom => new(style.BorderBottom, style.BorderBottomColor, style.BorderBottomStyle, origin),
            _ => new(style.BorderLeft, style.BorderLeftColor, style.BorderLeftStyle, origin)
        };

    /// <summary>
    /// Rewrites every box's borders to half the lines around it, and clears their colours.
    /// </summary>
    /// <remarks>
    /// A cell spanning several rows or columns takes the WIDEST line along each of its edges. The
    /// specification resolves each segment separately, which a single border box cannot express;
    /// the widest is what keeps the cell's content clear of every line it touches.
    /// </remarks>
    void Apply(TableGrid grid, LayoutBox table)
    {
        foreach (var cell in grid.Cells)
        {
            var lastRow = Math.Min(rows, cell.Row + cell.RowSpan) - 1;
            var lastColumn = Math.Min(columns, cell.Column + cell.ColumnSpan) - 1;

            var left = 0f;
            var right = 0f;

            for (var row = cell.Row; row <= lastRow; row++)
            {
                left = MathF.Max(left, vertical[cell.Column, row].Width);
                right = MathF.Max(right, vertical[lastColumn + 1, row].Width);
            }

            var top = 0f;
            var bottom = 0f;

            for (var column = cell.Column; column <= lastColumn; column++)
            {
                top = MathF.Max(top, horizontal[cell.Row, column].Width);
                bottom = MathF.Max(bottom, horizontal[lastRow + 1, column].Width);
            }

            Rewrite(cell.Box, top, right, bottom, left);
        }

        // A row and a group keep their space but paint nothing: their borders have already been
        // folded into the lines above.
        foreach (var row in grid.Rows)
        {
            Rewrite(row.Box, 0, 0, 0, 0);
        }

        foreach (var group in grid.Groups)
        {
            Rewrite(group, 0, 0, 0, 0);
        }

        Rewrite(
            table,
            Widest(horizontal, 0, columns),
            Widest(vertical, columns, rows),
            Widest(horizontal, rows, columns),
            Widest(vertical, 0, rows));
    }

    /// <summary>
    /// Gives one box its halved borders, and repoints the text that was sharing its style.
    /// </summary>
    /// <remarks>
    /// The repointing is not housekeeping. A text node takes its parent's style INSTANCE, and line
    /// layout uses that shared reference to tell the block's own text from an inline box of its
    /// own — which is what keeps a cell's <c>vertical-align: middle</c>, inherited so the cell can
    /// read it, from shifting the cell's text half an x-height. Replacing the style without
    /// replacing the text's copy breaks that reference and every collapsed cell comes out 0.77px
    /// too tall, which is exactly how this was found.
    /// </remarks>
    static void Rewrite(LayoutBox box, float top, float right, float bottom, float left)
    {
        var previous = box.Style;
        var halved = Halved(previous, top, right, bottom, left);

        box.Style = halved;

        for (var index = 0; index < box.Inlines.Count; index++)
        {
            if (ReferenceEquals(box.Inlines[index].Style, previous))
            {
                box.Inlines[index] = box.Inlines[index] with {Style = halved};
            }
        }
    }

    static float Widest(Edge[,] lines, int index, int count)
    {
        var widest = 0f;

        for (var along = 0; along < count; along++)
        {
            widest = MathF.Max(widest, lines[index, along].Width);
        }

        return widest;
    }

    static ComputedStyle Halved(ComputedStyle style, float top, float right, float bottom, float left) =>
        style with
        {
            BorderTop = top / 2,
            BorderRight = right / 2,
            BorderBottom = bottom / 2,
            BorderLeft = left / 2,
            BorderTopColor = null,
            BorderRightColor = null,
            BorderBottomColor = null,
            BorderLeftColor = null
        };

    /// <summary>
    /// The lines to paint, once the cells have been placed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each line is centred on the boundary the two boxes share, which after the halving above is
    /// exactly where their border boxes meet — so the geometry falls out of the placed cells rather
    /// than having to be tracked alongside them.
    /// </para>
    /// <para>
    /// Every line runs its FULL length, half a crossing line past each end, so no corner is left
    /// unpainted. That makes the crossings overlap, and the order settles them: the list comes back
    /// sorted by width so the wider line is painted last and owns the junction. Measured — where a
    /// 4px table border crosses a 2px row line, Chrome shows the table's colour, and drawing the
    /// horizontals last regardless puts the wrong one on top at every corner of the frame.
    /// </para>
    /// </remarks>
    public List<CollapsedLine> Lines(TableGrid grid)
    {
        var occupancy = Occupancy(grid, rows, columns);
        var lines = new List<(float Width, CollapsedLine Line)>();

        for (var row = 0; row < rows; row++)
        {
            for (var line = 0; line <= columns; line++)
            {
                var edge = vertical[line, row];

                if (edge is not {Width: > 0, Color: {} color} ||
                    Column(occupancy, row, line) is not {} x)
                {
                    continue;
                }

                var (top, bottom) = Extent(occupancy, row);
                var column = Math.Min(line, columns - 1);

                // Half the crossing line at each end, which is what fills the junctions.
                top -= horizontal[row, column].Width / 2;
                bottom += horizontal[row + 1, column].Width / 2;

                lines.Add((
                    edge.Width,
                    new(new(x - edge.Width / 2, top, edge.Width, bottom - top), color)));
            }
        }

        for (var line = 0; line <= rows; line++)
        {
            for (var column = 0; column < columns; column++)
            {
                var edge = horizontal[line, column];

                if (edge is not {Width: > 0, Color: {} color} ||
                    Row(occupancy, line, column) is not {} y ||
                    Span(occupancy, line, column) is not var (from, to))
                {
                    continue;
                }

                var row = Math.Min(line, rows - 1);

                from -= vertical[column, row].Width / 2;
                to += vertical[column + 1, row].Width / 2;

                lines.Add((
                    edge.Width,
                    new(new(from, y - edge.Width / 2, to - from, edge.Width), color)));
            }
        }

        return [.. lines.OrderBy(_ => _.Width).Select(_ => _.Line)];
    }

    /// <summary>The x a vertical line is centred on, taken from the cells either side of it.</summary>
    static float? Column(TableCell?[,] occupancy, int row, int line)
    {
        var columns = occupancy.GetLength(1);

        if (line < columns && occupancy[row, line] is {} right)
        {
            return right.Box.BorderBox.X;
        }

        return line > 0 && occupancy[row, line - 1] is {} left ? left.Box.BorderBox.Right : null;
    }

    /// <summary>The y a horizontal line is centred on.</summary>
    static float? Row(TableCell?[,] occupancy, int line, int column)
    {
        var rows = occupancy.GetLength(0);

        if (line < rows && occupancy[line, column] is {} below)
        {
            return below.Box.BorderBox.Y;
        }

        return line > 0 && occupancy[line - 1, column] is {} above ? above.Box.BorderBox.Bottom : null;
    }

    /// <summary>The vertical extent of a row, from its cells.</summary>
    static (float Top, float Bottom) Extent(TableCell?[,] occupancy, int row)
    {
        var top = float.MaxValue;
        var bottom = float.MinValue;

        for (var column = 0; column < occupancy.GetLength(1); column++)
        {
            if (occupancy[row, column] is not {} cell)
            {
                continue;
            }

            top = MathF.Min(top, cell.Box.BorderBox.Y);
            bottom = MathF.Max(bottom, cell.Box.BorderBox.Bottom);
        }

        return top > bottom ? (0, 0) : (top, bottom);
    }

    /// <summary>The horizontal extent of one column at one row boundary.</summary>
    static (float From, float To)? Span(TableCell?[,] occupancy, int line, int column)
    {
        var rows = occupancy.GetLength(0);

        var cell = line < rows ? occupancy[line, column] : null;
        cell ??= line > 0 ? occupancy[line - 1, column] : null;

        return cell is null ? null : (cell.Box.BorderBox.X, cell.Box.BorderBox.Right);
    }
}
