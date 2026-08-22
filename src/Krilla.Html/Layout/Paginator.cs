/// <summary>
/// Decides where the laid-out document is cut into pages.
/// </summary>
/// <remarks>
/// <para>
/// The document is laid out once, as a single column of unbounded height, and then sliced. That
/// ordering is what makes pagination cheap, and it is sound as long as nothing on a page can
/// depend on which page it landed on — true for the current feature set, and the reason running
/// headers, <c>position: fixed</c> and page-relative counters are not in it.
/// </para>
/// <para>
/// Lines are the unbreakable unit, and a table ROW is the one exception. Boxes are otherwise not:
/// a background or border spanning a break is clipped at the page edge and resumes on the next
/// page, which is what paged media does with overflow. Honouring <c>break-inside</c> and
/// orphans/widows would mean tracking box-level break opportunities, which slice one does not.
/// </para>
/// </remarks>
static class Paginator
{
    /// <summary>
    /// The Y position, in layout units, where each page's content starts.
    /// </summary>
    /// <remarks>
    /// Always at least one entry, at zero, so an empty document still produces one page rather
    /// than none — an empty PDF is not a valid document, and a blank page is the honest render of
    /// blank input.
    /// </remarks>
    public static List<float> PageTops(LayoutBox root, float pageHeight)
    {
        var tops = new List<float> {0};

        if (pageHeight <= 0)
        {
            return tops;
        }

        var units = Unbreakable(root);

        var documentHeight = Math.Max(
            root.BorderBox.Bottom,
            units.Count == 0 ? 0 : units.Max(_ => _.Bottom));

        var top = 0f;

        while (top + pageHeight < documentHeight)
        {
            top = NextTop(units, top, pageHeight);
            tops.Add(top);
        }

        return tops;
    }

    /// <summary>
    /// The document's unbreakable units, in document order: every line box, except that a table
    /// row is one unit and the lines inside it are not.
    /// </summary>
    /// <remarks>
    /// A row moves whole. Breaking at a line inside one instead lands the break below the cell's
    /// top padding, so the row resumes overleaf missing that padding and everything after it on
    /// the page sits high by exactly it — and the sliver of the row above the break is stranded at
    /// the foot of the page before. A browser's printer moves the row, and <c>page/table_break</c>
    /// measures the difference at six pixels.
    ///
    /// A row taller than the page is handled the same way a line taller than the page is, by
    /// <see cref="NextTop"/>: it overflows rather than moving forever.
    /// </remarks>
    static List<Rect> Unbreakable(LayoutBox root)
    {
        var units = new List<Rect>();
        Walk(root, covered: false);
        units.Sort((left, right) => left.Y.CompareTo(right.Y));
        return units;

        void Walk(LayoutBox box, bool covered)
        {
            if (box.Style.Display == DisplayKind.TableRow)
            {
                units.Add(box.BorderBox);

                // The row's own box stands in for every line under it, including the ones inside
                // a float in a cell: a cell establishes a formatting context, so it grows to
                // contain its floats and the row grows with it.
                covered = true;
            }
            else if (!covered)
            {
                foreach (var line in box.Lines)
                {
                    units.Add(line.Bounds);
                }
            }

            foreach (var child in box.Children)
            {
                Walk(child, covered);
            }

            foreach (var floated in box.Floats)
            {
                Walk(floated.Box, covered);
            }

            // An absolute box is placed against its containing block, which may be nowhere near
            // the row it was declared in, so no row stands in for one.
            foreach (var positioned in box.Positioned)
            {
                Walk(positioned.Box, covered: false);
            }
        }
    }

    /// <summary>
    /// Where the page after the one starting at <paramref name="top"/> begins.
    /// </summary>
    /// <remarks>
    /// A straddling unit moves whole to the next page, so the break goes at its top. When nothing
    /// straddles the boundary the break goes at the page edge itself — and that fallback is not a
    /// degenerate case, it is what carries a block taller than the page onto the next one. A tall
    /// <c>div</c> contains no lines at all, so there is no line-based candidate anywhere inside it;
    /// without the fallback the break lands after the whole block and everything between the page
    /// edge and the block's end is simply never drawn.
    /// </remarks>
    static float NextTop(List<Rect> units, float top, float pageHeight)
    {
        var limit = top + pageHeight;

        foreach (var unit in units)
        {
            // Units already on this page or an earlier one.
            if (unit.Y <= top || unit.Bottom <= limit)
            {
                continue;
            }

            // Beyond the boundary entirely: nothing straddles it, so the page ends at its edge.
            if (unit.Y > limit)
            {
                break;
            }

            // A unit taller than the page has nowhere better to go — moving it to the top of the
            // next page would leave it still not fitting, and it would move again forever. Let it
            // overflow and keep looking for one that can actually be moved.
            if (unit.Height > pageHeight)
            {
                continue;
            }

            return unit.Y;
        }

        return limit;
    }

    /// <summary>
    /// The total height of the laid-out document, in layout units.
    /// </summary>
    public static float DocumentHeight(LayoutBox root) =>
        root.BorderBox.Bottom;
}
