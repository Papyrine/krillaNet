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
    public static List<float> PageTops(LayoutBox root, float pageHeight, bool constrainRuns = false)
    {
        var tops = new List<float> {0};

        if (pageHeight <= 0)
        {
            return tops;
        }

        var units = Unbreakable(root);
        var forced = ForcedBreaks(root);
        var sides = forced.ToDictionary(_ => _.Position, _ => _.Kind);
        var positions = forced.Select(_ => _.Position).ToList();

        var documentHeight = Math.Max(
            root.BorderBox.Bottom,
            units.Count == 0 ? 0 : units.Max(_ => _.Bounds.Bottom));

        var top = 0f;

        // The second half of the condition is what a forced break adds. Without it the loop ends
        // as soon as the remaining content fits on one page, and a document asking to start a page
        // is short far more often than not: three boxes totalling 144px on a 1056px page still
        // want two pages if one of them said so.
        while (top + pageHeight < documentHeight ||
               (positions.Count > 0 && positions[^1] > top))
        {
            top = NextTop(units, positions, top, pageHeight, constrainRuns);
            tops.Add(top);

            // A break that named a sheet gets a blank page inserted whenever the page it landed on
            // is the wrong parity. The blank page is `top` repeated: its slice runs from `top` to
            // `top`, so it holds nothing but the canvas — which is what a blank page in a browser
            // holds too.
            if (sides.TryGetValue(top, out var kind) && Misplaced(kind, tops.Count))
            {
                tops.Add(top);
            }
        }

        return tops;
    }

    /// <summary>
    /// Whether a page of this number is the wrong sheet for the break that started it.
    /// </summary>
    /// <remarks>
    /// Page one is a RIGHT-hand sheet, which is the convention for a left-to-right book and what
    /// CSS's <c>recto</c> means — so odd pages are right-hand and even ones left. A break asking
    /// for the sheet it already landed on inserts nothing.
    /// </remarks>
    static bool Misplaced(BreakKind kind, int pageNumber) =>
        kind switch
        {
            BreakKind.Recto => pageNumber % 2 == 0,
            BreakKind.Verso => pageNumber % 2 == 1,
            _ => false
        };

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
    static List<(float Position, BreakKind Kind)> ForcedBreaks(LayoutBox root)
    {
        // Pre-order, so a box's descendants are exactly the entries between its own index and
        // `ends`. That is what makes "the next box after this subtree" an index rather than a
        // second walk back up the tree.
        var flow = new List<LayoutBox>();
        var ends = new List<int>();

        Collect(root);

        // Keyed by position, so two boxes asking for a page at the same point produce one break.
        // The kind of the LAST one to ask wins, which matches how a stylesheet's later rule wins
        // and is only reachable when two adjacent boxes disagree about the sheet.
        var breaks = new SortedDictionary<float, BreakKind>();

        for (var index = 0; index < flow.Count; index++)
        {
            var style = flow[index].Style;

            if (style.BreakBefore.Forces())
            {
                Add(flow[index].BorderBox.Y, style.BreakBefore);
            }

            // Nothing follows the last box in the document, so there is nothing to move onto a
            // page of its own and no break worth taking. A browser emits a trailing blank page
            // here; one blank page at the end of a converted document is the less useful answer.
            if (style.BreakAfter.Forces() &&
                ends[index] < flow.Count)
            {
                Add(flow[ends[index]].BorderBox.Y, style.BreakAfter);
            }
        }

        return [.. breaks.Select(_ => (_.Key, _.Value))];

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

        void Add(float y, BreakKind kind)
        {
            // A break at the very start of the document asks for the page that already exists, and
            // taking it would emit a blank first page. That is the usual shape of this mistake
            // rather than an exotic one: `page-break-before: always` on a section wrapper is
            // written to separate the sections, not to precede the first of them.
            if (y > 0)
            {
                breaks[y] = kind;
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
    static List<PageUnit> Unbreakable(LayoutBox root)
    {
        var units = new List<PageUnit>();
        Walk(root, covered: false);
        units.Sort((left, right) => left.Bounds.Y.CompareTo(right.Bounds.Y));
        return units;

        void Walk(LayoutBox box, bool covered)
        {
            // A row is one by rule and an `avoid` box is one by request, but they are the same
            // thing to the slice: a rectangle that moves whole, rather than a run of lines that
            // move one at a time.
            if (box.Style.Display == DisplayKind.TableRow ||
                box.Style.BreakInside == BreakKind.Avoid)
            {
                units.Add(new(box.BorderBox, null));

                // The row's own box stands in for every line under it, including the ones inside
                // a float in a cell: a cell establishes a formatting context, so it grows to
                // contain its floats and the row grows with it.
                covered = true;
            }
            else if (!covered)
            {
                // Each line carries which block generated it and where it sits in that block's
                // run, which is what `orphans` and `widows` need and what a flat sequence of
                // rectangles cannot say. A block of one line is exempt from both by construction:
                // a break can only fall inside a run of two or more.
                for (var index = 0; index < box.Lines.Count; index++)
                {
                    units.Add(new(
                        box.Lines[index].Bounds,
                        new(
                            index,
                            box.Lines.Count,
                            box.Style.Orphans,
                            box.Style.Widows,
                            box.Lines[0].Bounds.Y,
                            box.Lines[^1].Bounds.Bottom)));
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
    static float NextTop(
        List<PageUnit> units,
        List<float> forced,
        float top,
        float pageHeight,
        bool constrainRuns)
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
            var bounds = unit.Bounds;

            // Units already on this page or an earlier one.
            if (bounds.Y <= top || bounds.Bottom <= limit)
            {
                continue;
            }

            // Beyond the boundary entirely: nothing straddles it, so the page ends at its edge.
            if (bounds.Y > limit)
            {
                break;
            }

            // A unit taller than the page has nowhere better to go — moving it to the top of the
            // next page would leave it still not fitting, and it would move again forever. Let it
            // overflow and keep looking for one that can actually be moved.
            if (bounds.Height > pageHeight)
            {
                continue;
            }

            return constrainRuns
                ? Constrained(units, unit, top)
                : bounds.Y;
        }

        return limit;
    }

    /// <summary>
    /// Moves a break earlier when <c>orphans</c> or <c>widows</c> would otherwise be violated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reached only under <see cref="HtmlOptions.HonourOrphansAndWidows"/>, which is off by
    /// default — not because the properties are hard but because CHROMIUM DOES NOT IMPLEMENT THEM,
    /// and this engine is measured against Chromium. The corpus says so directly:
    /// <c>page/break_between_lines</c> holds a reference in which a three-line paragraph is broken
    /// after its second line, stranding one line overleaf under the initial <c>widows: 2</c>.
    /// </para>
    /// <para>
    /// Both properties are counts of LINES either side of the break, so both are answered from the
    /// same two numbers: how many of the block's lines are above the candidate, and how many below.
    /// Too few above breaks <c>orphans</c>, too few below breaks <c>widows</c>, and the fix for
    /// either is the same in kind — move the break earlier, which is the only direction available.
    /// </para>
    /// <para>
    /// Widows is satisfied by moving up to the line that leaves enough below. Orphans cannot be
    /// satisfied that way at all — moving earlier only removes lines from above — so the whole run
    /// goes to the next page, and that is also the fallback when satisfying widows would leave too
    /// few above. A block whose entire run is shorter than the two constraints together therefore
    /// always moves whole, which is what a browser does with a three-line paragraph under
    /// <c>orphans: 2; widows: 2</c>.
    /// </para>
    /// <para>
    /// The break is moved to the first LINE's top rather than to the block's border edge, because
    /// lines are what this slice deals in. The consequence is that a bordered block moved for
    /// widows is cut above its first line rather than above its border — visible only on a block
    /// whose padding is large enough to leave the border stranded, and the price of not giving the
    /// paginator a second notion of what a unit is.
    /// </para>
    /// </remarks>
    static float Constrained(List<PageUnit> units, PageUnit unit, float top)
    {
        if (unit.Group is not {} group)
        {
            return unit.Bounds.Y;
        }

        var above = group.Index;
        var below = group.Count - group.Index;

        // Nothing of the block is on this page: the break already falls at its start, so neither
        // constraint has anything to say.
        if (above == 0)
        {
            return unit.Bounds.Y;
        }

        if (above >= group.Orphans && below >= group.Widows)
        {
            return unit.Bounds.Y;
        }

        if (below < group.Widows)
        {
            // The line that leaves exactly `Widows` below it. Enough remains above only when the
            // run is long enough to hold both constraints at once.
            var wanted = group.Count - group.Widows;

            if (wanted >= group.Orphans && Line(units, group, wanted) is {} moved)
            {
                return moved;
            }
        }

        // Whole run overleaf. Guarded against a run that starts at or above the page top, where
        // moving to it would not advance and the loop in `PageTops` would never terminate.
        return group.FirstTop > top ? group.FirstTop : unit.Bounds.Y;
    }

    /// <summary>
    /// The top of one line of a block's run, found by its index.
    /// </summary>
    /// <remarks>
    /// Looked up rather than carried, because the units are sorted by position and a block's lines
    /// are interleaved with everything else at the same depth — a float beside a paragraph puts its
    /// own lines between two of the paragraph's. Matching on the run's extent and the index is what
    /// picks the right one without giving every block an identity of its own.
    /// </remarks>
    static float? Line(List<PageUnit> units, LineGroup group, int index)
    {
        foreach (var unit in units)
        {
            if (unit.Group is {} candidate &&
                candidate.Index == index &&
                candidate.FirstTop == group.FirstTop &&
                candidate.LastBottom == group.LastBottom)
            {
                return unit.Bounds.Y;
            }
        }

        return null;
    }

    /// <summary>
    /// The total height of the laid-out document, in layout units.
    /// </summary>
    public static float DocumentHeight(LayoutBox root) =>
        root.BorderBox.Bottom;

    /// <summary>
    /// One thing a page break has to respect: a rectangle, and where it sits in a run of lines.
    /// </summary>
    /// <param name="Bounds">The rectangle a break must not fall inside.</param>
    /// <param name="Group">
    /// Null for a rectangle that moves whole — a table row, or a box carrying
    /// <c>break-inside: avoid</c> — which has no lines either side of it to count.
    /// </param>
    readonly record struct PageUnit(Rect Bounds, LineGroup? Group);

    /// <summary>
    /// Where a line sits in the run its block generated, and what that block asks of a break.
    /// </summary>
    /// <param name="Index">Which line of the run this is, from zero.</param>
    /// <param name="Count">How many lines the run holds.</param>
    /// <param name="Orphans">The fewest that may be left above a break.</param>
    /// <param name="Widows">The fewest that may be carried below one.</param>
    /// <param name="FirstTop">
    /// The top of the run's first line, which is where the whole run moves to when neither
    /// constraint can be met. It doubles as half the run's identity, since the units are sorted by
    /// position rather than grouped by block.
    /// </param>
    /// <param name="LastBottom">The bottom of the run's last line, the other half of that identity.</param>
    readonly record struct LineGroup(
        int Index,
        int Count,
        int Orphans,
        int Widows,
        float FirstTop,
        float LastBottom);
}
