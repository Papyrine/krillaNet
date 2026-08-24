/// <summary>
/// Lays out a table: the separated border model, with columns sized from their content.
/// </summary>
/// <remarks>
/// <para>
/// A table is the one construct here whose parts cannot be laid out independently. A column's
/// width is a property of every cell in it, so no cell can be measured until all of them have
/// been, and a row's height is a property of every cell in the row. That is why this is a
/// formatting context of its own rather than a variation on <see cref="BlockLayout"/>: the order
/// is measure everything, decide, then place, where block layout can place as it goes.
/// </para>
/// <para>
/// The width rules are not in any specification in usable detail — CSS 2.1 §17.5.2 describes the
/// automatic algorithm as a sketch and leaves the distribution to the user agent. They were
/// measured out of Chrome instead, and they turn out to be two rules rather than one:
/// </para>
/// <list type="bullet">
/// <item>
/// A table wider than its max-content width gives each column a share proportional to its
/// max-content width.
/// </item>
/// <item>
/// A table between its min-content and max-content widths gives each column its min-content width
/// plus a share of the slack proportional to how much the column could grow.
/// </item>
/// </list>
/// <para>
/// Both reproduce Chrome to within a hundredth of a pixel across the cases the corpus measures.
/// The two are genuinely different distributions and using either alone is visibly wrong in the
/// other regime.
/// </para>
/// <para>
/// Only the separated border model is implemented. <c>border-collapse: collapse</c> lays out as
/// separated, which is a real difference rather than a rounding one — collapsed borders are
/// shared between neighbours and half of each sits outside the cell.
/// </para>
/// </remarks>
static class TableLayout
{
    /// <summary>
    /// Lays out <paramref name="table"/> with its border box starting at
    /// (<paramref name="x"/>, <paramref name="y"/>), and returns the border box height.
    /// </summary>
    /// <param name="table">The table box.</param>
    /// <param name="x">Left edge of the containing block's content box.</param>
    /// <param name="y">Top edge of the table's border box.</param>
    /// <param name="containingWidth">The containing block's content width.</param>
    /// <param name="fonts">The faces available for measuring text.</param>
    public static float Layout(LayoutBox table, float x, float y, float containingWidth, FontSet fonts)
    {
        var style = table.Style;
        var grid = TableGrid.Build(table);

        // Before anything measures a cell. The collapsing model rewrites every box's borders to
        // half the line beside it, and the column algorithm sizes columns from cell border-box
        // widths — so resolving afterwards would size the table with one set of borders and paint
        // it with another.
        var collapsed = CollapsedBorders.Resolve(grid, table);

        // Re-read, because the resolve above may have replaced the table's own borders with the
        // halves of its outer lines.
        style = table.Style;

        var paddingLeft = style.PaddingLeft.Resolve(containingWidth);
        var paddingRight = style.PaddingRight.Resolve(containingWidth);
        var paddingTop = style.PaddingTop.Resolve(containingWidth);
        var paddingBottom = style.PaddingBottom.Resolve(containingWidth);
        var surround = paddingLeft + paddingRight + style.BorderWidthX;

        // Collapsed tables have no spacing at all: the boundary a gap would sit in is where the
        // shared line goes instead.
        var spacingX = collapsed is null ? style.BorderSpacingX : 0;
        var spacingY = collapsed is null ? style.BorderSpacingY : 0;
        // No columns means no edge spacing. The separated model puts a gap outside the first and
        // last column, and with neither of them there is nothing for a gap to be outside of — so an
        // empty table is empty rather than two pixels square.
        var gaps = grid.ColumnCount == 0 ? 0 : (grid.ColumnCount + 1) * spacingX;

        var columns = MeasureColumns(grid, fonts, spacingX, style);
        var contentWidth = ResolveWidth(
            style,
            columns,
            gaps,
            CaptionMinimum(grid, fonts),
            containingWidth,
            surround);
        var widths = Distribute(columns, contentWidth - gaps);

        var marginLeft = ResolveMargin(style, containingWidth, surround, contentWidth);
        var borderBoxX = x + marginLeft;
        var contentX = borderBoxX + style.BorderLeft + paddingLeft;
        var contentY = y + style.BorderTop + paddingTop;

        // Column left edges, relative to the table's content box. The leading gap is the edge
        // spacing, which the separated model puts outside the first column as well as between.
        var columnX = new float[grid.ColumnCount + 1];
        columnX[0] = spacingX;

        for (var index = 0; index < grid.ColumnCount; index++)
        {
            columnX[index + 1] = columnX[index] + widths[index] + spacingX;
        }

        var top = 0f;

        if (grid.Caption is {} caption && style.CaptionSide == CaptionSideKind.Top)
        {
            top = BlockLayout.Layout(caption, contentX, contentY, contentWidth, fonts);
        }

        // The gap above the first row, which exists only when there is a row for it to be above.
        if (grid.Rows.Count > 0)
        {
            top += spacingY;
        }

        var natural = MeasureCells(grid, widths, contentWidth, spacingX, fonts);
        SetRowHeights(grid, natural, spacingY);
        PlaceRows(grid, ref top, spacingY);

        var contentHeight = top;

        // Below the grid, and below the trailing edge spacing the grid already added — measured
        // against Chrome, where a bottom caption sits exactly as far under the last row as a top
        // one sits above the first.
        if (grid.Caption is {} below && style.CaptionSide == CaptionSideKind.Bottom)
        {
            contentHeight += BlockLayout.Layout(
                below,
                contentX,
                contentY + contentHeight,
                contentWidth,
                fonts);
        }

        PlaceCells(grid, natural, widths, columnX, contentX, contentY, spacingX, spacingY);
        PlaceColumns(grid, widths, columnX, contentY);
        PlaceRowBoxes(grid, contentX, contentY, contentWidth, spacingX);

        var borderBoxWidth = contentWidth + surround;
        var borderBoxHeight = contentHeight + paddingTop + paddingBottom + style.BorderWidthY;

        table.BorderBox = new(borderBoxX, y, borderBoxWidth, borderBoxHeight);
        table.ContentBox = new(contentX, contentY, contentWidth, contentHeight);

        // After placement, since every line is centred on a boundary two placed cells share.
        table.CollapsedLines = collapsed?.Lines(grid);

        return borderBoxHeight;
    }

    /// <summary>
    /// The min-content and max-content widths of <paramref name="table"/>'s border box.
    /// </summary>
    /// <remarks>
    /// For a table nested inside something that has to size to it — a cell in an outer table, or a
    /// shrink-to-fit block. It runs the column measurement and stops, which is exactly the part of
    /// the algorithm that does not need a width to have been chosen.
    /// </remarks>
    public static (float Min, float Max) Intrinsic(LayoutBox table, FontSet fonts)
    {
        var style = table.Style;
        var grid = TableGrid.Build(table);
        var columns = MeasureColumns(grid, fonts, style.BorderSpacingX, style);

        var gaps = grid.ColumnCount == 0 ? 0 : (grid.ColumnCount + 1) * style.BorderSpacingX;
        var surround =
            style.PaddingLeft.Resolve(0) +
            style.PaddingRight.Resolve(0) +
            style.BorderWidthX;

        var minimum = Math.Max(columns.MinTotal + gaps, CaptionMinimum(grid, fonts));

        return (minimum + surround, Math.Max(minimum, columns.MaxTotal + gaps) + surround);
    }

    /// <summary>
    /// What each column can be, before any width has been chosen.
    /// </summary>
    /// <param name="Min">Narrowest each column can be without its content overflowing.</param>
    /// <param name="Max">Width each column would take given unlimited room.</param>
    /// <param name="Fixed">
    /// A width a cell declared outright, which pins the column rather than letting it share.
    /// </param>
    /// <param name="Percent">A width declared as a fraction of the space available to columns.</param>
    /// <param name="PercentSurround">
    /// Border and padding to add on top of a resolved percentage, which fixed layout needs and
    /// automatic layout does not. See <see cref="MeasureFirstRow"/>.
    /// </param>
    sealed record ColumnSizes(
        float[] Min,
        float[] Max,
        float?[] Fixed,
        float?[] Percent,
        float[] PercentSurround)
    {
        /// <summary>Narrowest the columns can be in total, declared widths included.</summary>
        /// <remarks>
        /// What a table SHRINK-WRAPS to, so a declared column width counts: a table with no width of
        /// its own is at least as wide as its columns asked to be.
        /// </remarks>
        public float MinTotal => Min.Zip(Fixed, (min, pinned) => Math.Max(min, pinned ?? 0)).Sum();

        /// <summary>Narrowest the columns can be on their CONTENT alone.</summary>
        /// <remarks>
        /// The floor under a table that declares its own width, where a declared column width does
        /// NOT raise it — measured: three columns declaring 220, 220 and 60 in a table declaring 480
        /// come out 207.5, 207.5 and 57.0, so the table keeps the width it asked for and the columns
        /// shrink to fit it. Using the pinned total here instead grew the table to 500 and left
        /// nothing to distribute; using the content total for the shrink-wrap case instead let a
        /// table collapse below the widths its columns declared, which is what
        /// <c>table/spans</c> caught.
        /// </remarks>
        public float ContentMinTotal => Min.Sum();

        /// <summary>Widest the columns want to be in total.</summary>
        public float MaxTotal => Max.Zip(Fixed, (max, pinned) => Math.Max(max, pinned ?? 0)).Sum();
    }

    /// <summary>
    /// Measures every column from the cells in it.
    /// </summary>
    /// <remarks>
    /// Cells spanning one column are taken first, and spanning cells afterwards, because a
    /// spanning cell can only be shared out once the columns it covers have a size to share it in
    /// proportion to. Spans are handled narrowest first for the same reason.
    /// </remarks>
    static ColumnSizes MeasureColumns(
        TableGrid grid,
        FontSet fonts,
        float spacingX,
        ComputedStyle table)
    {
        var count = grid.ColumnCount;
        var sizes = new ColumnSizes(
            new float[count],
            new float[count],
            new float?[count],
            new float?[count],
            new float[count]);

        if (count == 0)
        {
            return sizes;
        }

        // Fixed layout reads the first row and stops. That is the whole point of it: the columns
        // can be settled without measuring any content, so a long table lays out in one pass and
        // its columns do not shift as more rows arrive. It needs a declared table width to divide
        // up, and falls back to the automatic algorithm without one.
        if (table is {TableLayout: TableLayoutKind.Fixed, Width.IsAuto: false})
        {
            MeasureFirstRow(grid, sizes);
            return sizes;
        }

        // A <col> width applies to the whole column before any cell is consulted, and a cell's own
        // declared width then competes with it on the same terms — which is what makes a table with
        // both behave as though the column definition were a cell in every row.
        Declared(grid, sizes);

        foreach (var cell in grid.Cells.Where(_ => _.ColumnSpan == 1))
        {
            var (min, max) = IntrinsicWidths.Measure(cell.Box, fonts);
            var column = cell.Column;

            sizes.Min[column] = Math.Max(sizes.Min[column], min);
            sizes.Max[column] = Math.Max(sizes.Max[column], max);

            var width = cell.Box.Style.Width;

            if (width.Kind == LengthKind.Absolute)
            {
                sizes.Fixed[column] = Math.Max(sizes.Fixed[column] ?? 0, max);
            }
            else if (width.Kind == LengthKind.Percent)
            {
                sizes.Percent[column] = Math.Max(sizes.Percent[column] ?? 0, width.Value / 100f);
            }
        }

        foreach (var cell in grid.Cells.Where(_ => _.ColumnSpan > 1).OrderBy(_ => _.ColumnSpan))
        {
            var (min, max) = IntrinsicWidths.Measure(cell.Box, fonts);
            var inner = (cell.ColumnSpan - 1) * spacingX;

            Spread(sizes.Min, cell.Column, cell.ColumnSpan, min - inner);
            Spread(sizes.Max, cell.Column, cell.ColumnSpan, max - inner);
        }

        return sizes;
    }

    /// <summary>
    /// Folds the table's <c>&lt;col&gt;</c> widths into the column sizes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A column definition contributes a width and nothing else — no content, so no minimum and no
    /// maximum of its own. An absolute width becomes the column's fixed width and a percentage its
    /// percentage, exactly as a cell's declared width does; a column with more definitions than the
    /// grid has columns ignores the extras, which is what a browser does with a <c>&lt;colgroup&gt;</c>
    /// that over-counts.
    /// </para>
    /// <para>
    /// Read for BOTH algorithms. Fixed layout is where <c>&lt;col&gt;</c> is most often used and
    /// automatic layout is where it is most often written by accident, and the specification gives
    /// the width to the column either way.
    /// </para>
    /// </remarks>
    static void Declared(TableGrid grid, ColumnSizes sizes)
    {
        var columns = grid.Table.Columns;

        for (var index = 0; index < columns.Count && index < sizes.Min.Length; index++)
        {
            if (columns[index].Kind == LengthKind.Absolute)
            {
                sizes.Fixed[index] = Math.Max(sizes.Fixed[index] ?? 0, columns[index].Value);
            }
            else if (columns[index].Kind == LengthKind.Percent)
            {
                sizes.Percent[index] = Math.Max(sizes.Percent[index] ?? 0, columns[index].Value / 100f);
            }
        }
    }

    /// <summary>
    /// Sizes the columns from the first row alone, for fixed layout.
    /// </summary>
    /// <remarks>
    /// A column whose first-row cell declares no width is left wanting nothing at all, rather than
    /// wanting its content. That is what makes the leftover space divide equally between the auto
    /// columns instead of in proportion to what is in them, and it is the visible difference
    /// between the two algorithms.
    /// </remarks>
    static void MeasureFirstRow(TableGrid grid, ColumnSizes sizes)
    {
        Declared(grid, sizes);

        foreach (var cell in grid.Cells.Where(_ => _.Row == 0))
        {
            var width = cell.Box.Style.Width;

            // A spanning cell in the first row declares a width for the columns together, and
            // splitting it between them needs a rule that only matters for a construct the corpus
            // does not cover. Leaving them auto shares the space evenly, which is at least stable.
            if (cell.ColumnSpan != 1)
            {
                continue;
            }

            if (width.Kind == LengthKind.Absolute)
            {
                var surround = cell.Box.Style.SurroundX(0);
                sizes.Fixed[cell.Column] = cell.Box.Style.ContentSize(width.Value, surround) + surround;
            }
            else if (width.Kind == LengthKind.Percent)
            {
                sizes.Percent[cell.Column] = width.Value / 100f;

                // The one place the two algorithms read a percentage differently, and it was
                // measured rather than reasoned about. Automatic layout treats the percentage as
                // the whole column — border, padding and all — because it has to compete with
                // content widths, which are border-box. Fixed layout has no content to compete
                // with, so the percentage is the cell's `width` under ordinary content-box sizing
                // and the padding is added on top. The difference is exactly the cell's padding,
                // which is small enough to look like a rounding error and is not one.
                sizes.PercentSurround[cell.Column] = cell.Box.Style.SurroundX(0);
            }
        }
    }

    /// <summary>
    /// Widens the columns a spanning cell covers until they can hold it.
    /// </summary>
    /// <remarks>
    /// The shortfall is shared in proportion to what the columns already want, which is what
    /// Chrome does and is not the obvious choice — sharing it equally is, and it puts a wide
    /// spanning cell's extra width into a column that had nothing in it. Columns that all want
    /// nothing are the one case where equal shares are right, because there is no proportion to
    /// go by.
    /// </remarks>
    static void Spread(float[] sizes, int start, int span, float required)
    {
        var current = 0f;

        for (var index = start; index < start + span; index++)
        {
            current += sizes[index];
        }

        if (required <= current)
        {
            return;
        }

        var extra = required - current;

        for (var index = start; index < start + span; index++)
        {
            sizes[index] += current > 0 ? extra * sizes[index] / current : extra / span;
        }
    }

    /// <summary>
    /// The table's content width.
    /// </summary>
    /// <remarks>
    /// A declared width on a table is normally a BORDER-box width — the user-agent stylesheet
    /// gives tables <c>box-sizing: border-box</c>, so <c>width: 300px</c> with a 10px border
    /// leaves 280 for content rather than making the table 320 wide. That comes from the rule
    /// rather than from table layout, which is why it goes through <c>box-sizing</c> like every
    /// other declared width: an author who writes <c>content-box</c> on a table gets 320, as
    /// Chrome gives.
    ///
    /// It is also a minimum rather than an instruction: a table never renders narrower than its
    /// columns' min-content widths, however narrow the declaration.
    /// </remarks>
    static float ResolveWidth(
        ComputedStyle style,
        ColumnSizes columns,
        float gaps,
        float captionMinimum,
        float containingWidth,
        float surround)
    {
        if (style.Width.ResolveOrNull(containingWidth) is {} declared)
        {
            return Math.Max(
                style.ContentSize(declared, surround),
                Math.Max(columns.ContentMinTotal + gaps, captionMinimum));
        }

        var minimum = Math.Max(columns.MinTotal + gaps, captionMinimum);

        var available =
            containingWidth -
            surround -
            style.MarginLeft.Resolve(containingWidth) -
            style.MarginRight.Resolve(containingWidth);

        // Shrink to fit: as wide as the content wants, but not wider than there is room for, and
        // never narrower than the content can survive.
        return Math.Clamp(available, minimum, Math.Max(minimum, columns.MaxTotal + gaps));
    }

    /// <summary>
    /// Shares a surplus or a shortfall among columns that are all pinned.
    /// </summary>
    /// <remarks>
    /// <para>
    /// TWO RULES, which is the same shape the column algorithm already has and was measured the same
    /// way. A SURPLUS is shared in proportion to the widths themselves: three columns wanting 220,
    /// 60 and 60 with 132 left over come out 305.4, 83.3 and 83.3, each grown by its own share. A
    /// SHORTFALL is shared in proportion to what each column can GIVE UP — its width less its
    /// min-content width — so three wanting 220, 220 and 60 with 28 too many come out 207.5, 207.5
    /// and 57.0 rather than the 207.7, 207.7 and 56.6 that shrinking by width gives.
    /// </para>
    /// <para>
    /// The difference is four tenths of a pixel on this arrangement and it is the whole of what
    /// separates the two readings, which is why it took a scenario to find: the wide columns give up
    /// nearly all of the shortfall either way, and only the narrow one moves.
    /// </para>
    /// <para>
    /// Unreachable until <c>&lt;col&gt;</c> arrived. A table whose columns are ALL pinned needs every
    /// one of them declared, and a cell declaring a width in every column of every row is rare
    /// enough that no scenario had it.
    /// </para>
    /// </remarks>
    static void Reconcile(ColumnSizes columns, float[] widths, float remaining)
    {
        if (Math.Abs(remaining) <= 0.001f)
        {
            return;
        }

        var shares = new float[widths.Length];

        for (var index = 0; index < widths.Length; index++)
        {
            shares[index] = remaining > 0
                ? widths[index]
                : Math.Max(0, widths[index] - columns.Min[index]);
        }

        var total = shares.Sum();

        if (total <= 0)
        {
            return;
        }

        // Allocated on Chrome's 1/64 pixel grid, with the LAST column taking whatever is left over.
        // Sharing the remainder evenly instead leaves two identical columns identical, and the
        // browser does not: two columns both wanting 60 come out 83.28 and 83.31, three hundredths
        // apart, because the second is handed the residue. That is small enough to look like noise
        // and is the difference between a scenario that is exact and one that is nearly exact.
        const float grid = 64;

        var target = (long) MathF.Round((widths.Sum() + remaining) * grid);
        var assigned = 0L;

        for (var index = 0; index < widths.Length - 1; index++)
        {
            var wanted = Math.Max(
                columns.Min[index],
                widths[index] + remaining * shares[index] / total);

            var units = (long) MathF.Floor(wanted * grid);

            widths[index] = units / grid;
            assigned += units;
        }

        widths[^1] = Math.Max(columns.Min[^1], (target - assigned) / grid);
    }

    /// <summary>
    /// The width the caption forces the table to be at least.
    /// </summary>
    /// <remarks>
    /// Its MIN-content width, not its maximum: a table is never narrower than its caption's
    /// longest word, but a long caption wraps rather than stretching the table out to hold it on
    /// one line. Both halves were measured — a caption of two words and one of eight produced
    /// tables of exactly the same width, because they share their longest word.
    /// </remarks>
    static float CaptionMinimum(TableGrid grid, FontSet fonts)
    {
        if (grid.Caption is not {} caption)
        {
            return 0;
        }

        return IntrinsicWidths.Measure(caption, fonts).Min +
               caption.Style.MarginLeft.Resolve(0) +
               caption.Style.MarginRight.Resolve(0);
    }

    /// <summary>
    /// Shares <paramref name="available"/> among the columns.
    /// </summary>
    static float[] Distribute(ColumnSizes columns, float available)
    {
        var count = columns.Min.Length;
        var widths = new float[count];
        var free = new List<int>();
        var remaining = available;

        for (var index = 0; index < count; index++)
        {
            var pinned = columns.Percent[index] is {} percent
                ? percent * available + columns.PercentSurround[index]
                : columns.Fixed[index];

            if (pinned is {} width)
            {
                widths[index] = Math.Max(width, columns.Min[index]);
                remaining -= widths[index];
                continue;
            }

            free.Add(index);
        }

        // Every column pinned, and the pinned widths do not add up to the table. The surplus or the
        // shortfall is shared in PROPORTION to the widths themselves, which is measured rather than
        // reasoned: three columns wanting 220, 60 and 60 in a 480px table come out 305.4, 83.3 and
        // 83.3 — each grown by its own share of the 140 left over — and three wanting 220, 220 and
        // 60 in the same table come out 207.5, 207.5 and 57.0, each shrunk by its share of the 20 too
        // many.
        //
        // Unreachable until <col> arrived: a table whose columns are all pinned needs every one of
        // them declared, and a cell declaring a width in every column of every row is rare enough
        // that no scenario had it. `table/columns` has three.
        if (free.Count == 0)
        {
            Reconcile(columns, widths, remaining);
            return widths;
        }

        var minTotal = free.Sum(_ => columns.Min[_]);
        var maxTotal = free.Sum(_ => columns.Max[_]);

        if (remaining <= minTotal)
        {
            // Nothing to share, and the table is already as narrow as it can be. The columns take
            // their minimums and the table overflows, which is what a browser does rather than
            // shrinking text below what fits.
            foreach (var index in free)
            {
                widths[index] = columns.Min[index];
            }

            return widths;
        }

        if (remaining >= maxTotal)
        {
            // Wider than the content wants: every column gets a share proportional to what it
            // wanted. Handing each its maximum and dumping the surplus on the last column would
            // also fill the row, and looks nothing like a browser.
            //
            // Never below the column's own maximum, which the arithmetic already guarantees and
            // the arithmetic in floating point does not. A shrink-to-fit table lands on exactly
            // this branch with nothing to spare, and the multiply-then-divide round trip loses a
            // hundredth of a pixel — enough for the last word in the widest cell to stop fitting,
            // which wraps it and makes the table a whole line taller.
            foreach (var index in free)
            {
                widths[index] = maxTotal > 0
                    ? Math.Max(columns.Max[index], remaining * columns.Max[index] / maxTotal)
                    : remaining / free.Count;
            }

            return widths;
        }

        var slack = remaining - minTotal;
        var growth = maxTotal - minTotal;

        foreach (var index in free)
        {
            var room = columns.Max[index] - columns.Min[index];
            widths[index] = columns.Min[index] + (growth > 0 ? slack * room / growth : 0);
        }

        return widths;
    }

    /// <summary>
    /// The table's left margin, with the auto rules a shrink-to-fit box takes.
    /// </summary>
    static float ResolveMargin(
        ComputedStyle style,
        float containingWidth,
        float surround,
        float contentWidth)
    {
        var slack = Math.Max(0, containingWidth - surround - contentWidth);
        var marginLeft = style.MarginLeft.ResolveOrNull(containingWidth);
        var marginRight = style.MarginRight.ResolveOrNull(containingWidth);

        return (marginLeft, marginRight) switch
        {
            (null, null) => slack / 2,
            (null, not null) => Math.Max(0, slack - marginRight.Value),
            _ => marginLeft.Value
        };
    }

    /// <summary>
    /// Lays out every cell at its column width, returning the height each came out.
    /// </summary>
    /// <remarks>
    /// Laid out at the origin and moved later, because where a cell ends up depends on row heights
    /// that are not known until every cell in every row has been measured. Doing it in this order
    /// is what lets a cell's own content decide its row's height.
    /// </remarks>
    static CellHeight[] MeasureCells(
        TableGrid grid,
        float[] widths,
        float contentWidth,
        float spacingX,
        FontSet fonts)
    {
        var natural = new CellHeight[grid.Cells.Count];

        for (var index = 0; index < grid.Cells.Count; index++)
        {
            var cell = grid.Cells[index];
            var used = BlockLayout.Layout(
                cell.Box,
                0,
                0,
                contentWidth,
                fonts,
                CellWidth(cell, widths, spacingX));

            natural[index] = new(used, ContentExtent(cell.Box));
        }

        return natural;
    }

    /// <param name="Used">The cell's border-box height, which is what a row has to hold.</param>
    /// <param name="Content">How much of it the content actually occupies.</param>
    readonly record struct CellHeight(float Used, float Content);

    /// <summary>
    /// How far a cell's content actually reaches below its content edge.
    /// </summary>
    /// <remarks>
    /// Not the same as the height layout gave the cell, and the difference is the whole reason this
    /// exists: a cell with <c>height: 100px</c> holding one line of text is a hundred pixels tall
    /// and its content is eighteen. Centring the height rather than the content leaves the text
    /// against the top edge, looking as though vertical alignment were not implemented at all.
    /// </remarks>
    static float ContentExtent(LayoutBox box)
    {
        var bottom = box.ContentBox.Y;

        foreach (var line in box.Lines)
        {
            bottom = Math.Max(bottom, line.Bounds.Bottom);
        }

        foreach (var child in box.Children)
        {
            bottom = Math.Max(bottom, child.BorderBox.Bottom);
        }

        return Math.Max(0, bottom - box.ContentBox.Y);
    }

    /// <summary>The border-box width a cell gets: its columns, and the gaps it spans across.</summary>
    static float CellWidth(TableCell cell, float[] widths, float spacingX)
    {
        var width = (cell.ColumnSpan - 1) * spacingX;

        for (var index = cell.Column; index < cell.Column + cell.ColumnSpan; index++)
        {
            width += widths[index];
        }

        return width;
    }

    /// <summary>
    /// Decides how tall each row is.
    /// </summary>
    /// <remarks>
    /// Cells spanning one row settle it; a cell spanning several can only widen the rows it covers
    /// if they cannot already hold it, and the shortfall is then shared equally between them.
    /// Equally rather than proportionally, which is the opposite of the column rule and is what
    /// Chrome does — measured, not assumed.
    /// </remarks>
    static void SetRowHeights(TableGrid grid, CellHeight[] natural, float spacingY)
    {
        foreach (var row in grid.Rows)
        {
            row.Height = row.Box.Style.Height.Kind == LengthKind.Absolute
                ? Math.Max(0, row.Box.Style.Height.Value)
                : 0;
        }

        for (var index = 0; index < grid.Cells.Count; index++)
        {
            var cell = grid.Cells[index];

            if (cell.RowSpan == 1)
            {
                var row = grid.Rows[cell.Row];
                row.Height = Math.Max(row.Height, natural[index].Used);
            }
        }

        for (var index = 0; index < grid.Cells.Count; index++)
        {
            var cell = grid.Cells[index];
            if (cell.RowSpan == 1)
            {
                continue;
            }

            var available = SpannedHeight(grid, cell, spacingY);
            if (natural[index].Used <= available)
            {
                continue;
            }

            var extra = (natural[index].Used - available) / cell.RowSpan;

            for (var row = cell.Row; row < cell.Row + cell.RowSpan; row++)
            {
                grid.Rows[row].Height += extra;
            }
        }

    }

    /// <summary>The height a cell has across the rows it spans, gaps included.</summary>
    static float SpannedHeight(TableGrid grid, TableCell cell, float spacingY)
    {
        var height = (cell.RowSpan - 1) * spacingY;

        for (var row = cell.Row; row < cell.Row + cell.RowSpan; row++)
        {
            height += grid.Rows[row].Height;
        }

        return height;
    }

    /// <summary>Stacks the rows, advancing <paramref name="top"/> past the last of them.</summary>
    static void PlaceRows(TableGrid grid, ref float top, float spacingY)
    {
        foreach (var row in grid.Rows)
        {
            row.Y = top;
            top += row.Height + spacingY;
        }
    }

    /// <summary>
    /// Puts each cell where its column and row say, and aligns its content within it.
    /// </summary>
    /// <remarks>
    /// A cell is stretched to its row's height whatever its content came to, so the content has to
    /// be moved down inside it — which is what <c>vertical-align</c> decides, and why cells are
    /// laid out at the origin and translated rather than laid out in place.
    /// </remarks>
    /// <summary>
    /// Gives each <c>&lt;col&gt;</c> and <c>&lt;colgroup&gt;</c> the rectangle a browser reports for
    /// it.
    /// </summary>
    /// <remarks>
    /// Its columns' extent by the height of the ROW AREA — measured: a <c>&lt;col&gt;</c> comes back
    /// with exactly its column's x and width, and the height and y of the section holding the rows.
    /// A group covers its columns from the first one's left edge to the last one's right, which
    /// includes the spacing between them.
    /// </remarks>
    static void PlaceColumns(TableGrid grid, float[] widths, float[] columnX, float contentY)
    {
        if (grid.Table.ColumnBoxes.Count == 0 || grid.Rows.Count == 0)
        {
            return;
        }

        var top = contentY + grid.Rows[0].Y;
        var height = grid.Rows[^1].Y + grid.Rows[^1].Height - grid.Rows[0].Y;

        foreach (var column in grid.Table.ColumnBoxes)
        {
            if (column.First >= widths.Length || column.Span <= 0)
            {
                // More definitions than the grid has columns. A browser reports nothing for the
                // extras, and a zero rectangle would be reported as a difference.
                column.Bounds = new(0, 0, 0, 0);
                continue;
            }

            var last = Math.Min(column.First + column.Span, widths.Length) - 1;
            var left = columnX[column.First];

            column.Bounds = new(left, top, columnX[last] + widths[last] - left, height);
        }
    }

    static void PlaceCells(
        TableGrid grid,
        CellHeight[] natural,
        float[] widths,
        float[] columnX,
        float contentX,
        float contentY,
        float spacingX,
        float spacingY)
    {
        for (var index = 0; index < grid.Cells.Count; index++)
        {
            var cell = grid.Cells[index];
            var style = cell.Box.Style;

            var width = CellWidth(cell, widths, spacingX);
            var height = SpannedHeight(grid, cell, spacingY);

            var left = contentX + columnX[cell.Column];
            var topEdge = contentY + grid.Rows[cell.Row].Y;

            var vertical = style.PaddingTop.Resolve(width) +
                           style.PaddingBottom.Resolve(width) +
                           style.BorderWidthY;

            var room = Math.Max(0, height - vertical);
            var content = natural[index].Content;

            var offset = style.VerticalAlign switch
            {
                VerticalAlignKind.Middle => (room - content) / 2,
                VerticalAlignKind.Bottom => room - content,
                // Baseline aligns a row's cells against each other's first baselines, which needs a
                // pass this does not have. Falling back to the top edge is what Chrome renders for
                // a row with one baseline-aligned cell, and is never further out than the row is
                // tall.
                _ => 0
            };

            cell.Box.Translate(left, topEdge + Math.Max(0, offset));

            // Set after translating, because the translate moved these too and the stretched
            // height is not what layout gave them.
            cell.Box.BorderBox = new(left, topEdge, width, height);
            cell.Box.ContentBox = cell.Box.BorderBox.Deflate(
                style.BorderTop + style.PaddingTop.Resolve(width),
                style.BorderRight + style.PaddingRight.Resolve(width),
                style.BorderBottom + style.PaddingBottom.Resolve(width),
                style.BorderLeft + style.PaddingLeft.Resolve(width));
        }
    }

    /// <summary>
    /// Gives the rows and row groups the boxes a browser reports for them.
    /// </summary>
    /// <remarks>
    /// Both span the grid rather than their own content: a row reaches from the first column's
    /// left edge to the last column's right, and a group from its first row's top to its last
    /// row's bottom. They exist to be compared against the browser and to carry a background, and
    /// neither would be right if they were sized from what is inside them.
    /// </remarks>
    static void PlaceRowBoxes(
        TableGrid grid,
        float contentX,
        float contentY,
        float contentWidth,
        float spacingX)
    {
        // No columns means no edge spacing, for the reason the table's own width uses: the
        // separated model puts a gap OUTSIDE the first and last columns, and with no columns there
        // is nothing for it to be outside of. Adding it anyway leaves the section of an empty
        // table two pixels along from an origin that occupies nothing.
        var edge = grid.ColumnCount == 0 ? 0 : spacingX;
        var left = contentX + edge;
        var width = Math.Max(0, contentWidth - 2 * edge);

        foreach (var row in grid.Rows)
        {
            row.Box.BorderBox = new(left, contentY + row.Y, width, row.Height);
            row.Box.ContentBox = row.Box.BorderBox;
        }

        foreach (var group in grid.Groups)
        {
            var owned = grid.Rows.Where(_ => ReferenceEquals(_.Group, group)).ToList();
            if (owned.Count == 0)
            {
                group.BorderBox = new(left, contentY, width, 0);
                group.ContentBox = group.BorderBox;
                continue;
            }

            var top = owned[0].Y;
            var bottom = owned[^1].Y + owned[^1].Height;

            group.BorderBox = new(left, contentY + top, width, bottom - top);
            group.ContentBox = group.BorderBox;
        }
    }
}
