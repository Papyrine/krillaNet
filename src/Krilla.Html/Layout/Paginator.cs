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
/// Lines are the unbreakable unit, and a box is one where it asks to be: a table ROW always, and
/// anything carrying <c>break-inside: avoid</c>. Boxes are otherwise not, so a background or
/// border spanning a break is clipped at the page edge and resumes on the next page, which is what
/// paged media does with overflow. Orphans and widows are still out of reach: they constrain how
/// many lines of a block may sit either side of a break, which needs the lines grouped by the
/// block that generated them rather than treated as one flat sequence.
/// </para>
/// <para>
/// A forced break — <c>break-before</c> or <c>break-after</c> — is the one thing here that adds
/// a page rather than choosing where an existing one ends, so the page count no longer follows
/// from the document height alone.
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
        var forced = ForcedBreaks(root);

        var documentHeight = Math.Max(
            root.BorderBox.Bottom,
            units.Count == 0 ? 0 : units.Max(_ => _.Bottom));

        var top = 0f;

        // The second half of the condition is what a forced break adds. Without it the loop ends
        // as soon as the remaining content fits on one page, and a document asking to start a page
        // is short far more often than not: three boxes totalling 144px on a 1056px page still
        // want two pages if one of them said so.
        while (top + pageHeight < documentHeight ||
               (forced.Count > 0 && forced[^1] > top))
        {
            top = NextTop(units, forced, top, pageHeight);
            tops.Add(top);
        }

        return tops;
    }

    /// <summary>
    /// The positions a page is required to start at, ascending and without duplicates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both properties reduce to the same thing — a page starts at some box's top border edge —
    /// which is what lets one list carry them. <c>break-before</c> names that box directly.
    /// <c>break-after</c> names the box before it, and resolves to the top of the next in-flow box
    /// in document order rather than to the declaring box's own bottom edge.
    /// </para>
    /// <para>
    /// Those two are not the same point, and which one is right was measured rather than reasoned:
    /// with a 40px margin between the boxes, Chrome starts the next page at the following box's
    /// top and the margin is simply gone. Breaking at the bottom edge instead puts every box on
    /// that page forty pixels low, under a strip of margin at the top of a page that should begin
    /// with content. <c>page/break_after</c> carries that margin for exactly this reason.
    /// </para>
    /// <para>
    /// Only in-flow boxes take part. A float or an absolutely positioned box is not at a flow
    /// position, so a break "before" one names nothing a page could start at, and CSS excludes
    /// them for the same reason. An inline-block is excluded by the same walk, since it hangs off
    /// a line rather than off <see cref="LayoutBox.Children"/> — and these properties apply to
    /// block-level boxes.
    /// </para>
    /// </remarks>
    static List<float> ForcedBreaks(LayoutBox root)
    {
        // Pre-order, so a box's descendants are exactly the entries between its own index and
        // `ends`. That is what makes "the next box after this subtree" an index rather than a
        // second walk back up the tree.
        var flow = new List<LayoutBox>();
        var ends = new List<int>();

        Collect(root);

        var breaks = new SortedSet<float>();

        for (var index = 0; index < flow.Count; index++)
        {
            var style = flow[index].Style;

            if (style.BreakBefore == BreakKind.Always)
            {
                Add(flow[index].BorderBox.Y);
            }

            // Nothing follows the last box in the document, so there is nothing to move onto a
            // page of its own and no break worth taking. A browser emits a trailing blank page
            // here; one blank page at the end of a converted document is the less useful answer.
            if (style.BreakAfter == BreakKind.Always &&
                ends[index] < flow.Count)
            {
                Add(flow[ends[index]].BorderBox.Y);
            }
        }

        return [.. breaks];

        void Collect(LayoutBox box)
        {
            var index = flow.Count;
            flow.Add(box);
            ends.Add(0);

            foreach (var child in box.Children)
            {
                Collect(child);
            }

            ends[index] = flow.Count;
        }

        void Add(float y)
        {
            // A break at the very start of the document asks for the page that already exists, and
            // taking it would emit a blank first page. That is the usual shape of this mistake
            // rather than an exotic one: `page-break-before: always` on a section wrapper is
            // written to separate the sections, not to precede the first of them.
            if (y > 0)
            {
                breaks.Add(y);
            }
        }
    }

    /// <summary>
    /// The document's unbreakable units, in document order: every line box, except that a table
    /// row is one unit and the lines inside it are not.
    /// </summary>
    /// <remarks>
    /// A row moves whole, and so does a box carrying <c>break-inside: avoid</c> — which reaches
    /// this by the same route and inherits the same answer for the case where it does not fit.
    ///
    /// Breaking at a line inside a row instead lands the break below the cell's
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
            // A row is one by rule and an `avoid` box is one by request, but they are the same
            // thing to the slice: a rectangle that moves whole, rather than a run of lines that
            // move one at a time.
            if (box.Style.Display == DisplayKind.TableRow ||
                box.Style.BreakInside == BreakKind.Avoid)
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
    /// A forced break inside the page wins outright: it is a request for a page to end here,
    /// rather than a preference about where an unavoidable break falls, so it is taken even though
    /// the page has room left. Only when there is none does a straddling unit decide.
    ///
    /// A straddling unit moves whole to the next page, so the break goes at its top. When nothing
    /// straddles the boundary the break goes at the page edge itself — and that fallback is not a
    /// degenerate case, it is what carries a block taller than the page onto the next one. A tall
    /// <c>div</c> contains no lines at all, so there is no line-based candidate anywhere inside it;
    /// without the fallback the break lands after the whole block and everything between the page
    /// edge and the block's end is simply never drawn.
    /// </remarks>
    static float NextTop(List<Rect> units, List<float> forced, float top, float pageHeight)
    {
        var limit = top + pageHeight;

        foreach (var position in forced)
        {
            // Behind us, on a page already decided.
            if (position <= top)
            {
                continue;
            }

            // Ascending, so the first one past the boundary means every later one is too, and it
            // belongs to a page this call is not deciding.
            if (position > limit)
            {
                break;
            }

            return position;
        }

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
