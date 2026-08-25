/// <summary>
/// Decides where the laid-out document is cut into pages.
/// </summary>
/// <remarks>
/// <para>
/// The document is laid out once, as a single column of unbounded height, and then sliced. That
/// ordering is what makes pagination cheap, and it survives the two things that DO depend on which
/// page they landed on, because neither changes a measurement: a <c>position: fixed</c> box is
/// drawn again through a translate, and a repeating table header takes a band at the top of a
/// continuation page — which shortens the slice rather than moving anything in the tree.
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
    /// Where each page's content starts, and what is re-drawn above it.
    /// </summary>
    /// <remarks>
    /// Always at least one entry, at zero, so an empty document still produces one page rather
    /// than none — an empty PDF is not a valid document, and a blank page is the honest render of
    /// blank input.
    /// </remarks>
    public static List<PageStart> Paginate(LayoutBox root, float pageHeight, bool constrainRuns = false)
    {
        var pages = new List<PageStart> {PageStart.At(0)};

        if (pageHeight <= 0)
        {
            return pages;
        }

        var units = Unbreakable(root);
        var (forced, avoided) = Breaks(root);
        var sides = forced.ToDictionary(_ => _.Position, _ => _.Kind);
        var positions = forced.Select(_ => _.Position).ToList();
        var (headers, footers) = RepeatedGroups(root);

        var documentHeight = DocumentHeight(root, units);

        var top = 0f;

        // How much of the page the CURRENT one has left for document content. A page repeating a
        // table header holds less, which is what makes this a variable rather than `pageHeight`
        // at every use below.
        var available = pageHeight;

        while (true)
        {
            // Where the page would end if nothing were reserved at its foot, which is what decides
            // whether anything IS. The order is not circular: reserving can only move the end
            // earlier, and a table that continued past the later end continues past the earlier
            // one too — so one refinement settles it.
            var end = NextTop(units, positions, avoided, top, available, constrainRuns);
            var feet = FootersAt(footers, top, end, pageHeight);
            var reserved = feet.Sum(_ => _.Band.Height);

            if (reserved > 0)
            {
                available -= reserved;
                end = NextTop(units, positions, avoided, top, available, constrainRuns);
            }

            // Stamped onto the page this loop has been measuring rather than the one it is about
            // to start, because a footer belongs to the page whose content it follows.
            pages[^1] = pages[^1] with {ReservedBottom = reserved, Footers = feet};

            // The second half is what a forced break adds. Without it the loop ends as soon as the
            // remaining content fits on one page, and a document asking to start a page is short
            // far more often than not: three boxes totalling 144px on a 1056px page still want two
            // pages if one of them said so.
            if (top + available >= documentHeight &&
                (positions.Count == 0 || positions[^1] <= top))
            {
                return pages;
            }

            top = end;

            // A break that named a sheet gets a blank page inserted whenever the page it landed on
            // is the wrong parity. The blank page is `top` repeated: its slice runs from `top` to
            // `top`, so it holds nothing but the canvas — which is what a blank page in a browser
            // holds too, and why it repeats no header.
            if (sides.TryGetValue(top, out var kind) && Misplaced(kind, pages.Count + 1))
            {
                pages.Add(PageStart.At(top));
            }

            var repeated = HeadersAt(headers, top, pageHeight);

            pages.Add(new(top, repeated.Sum(_ => _.Band.Height), repeated, 0, []));
            available = pageHeight - pages[^1].Reserved;
        }
    }

    /// <summary>
    /// How far down the document reaches, which is how many pages it takes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The deepest BOX rather than the root's own edge, and the difference is a trailing margin.
    /// The root's margins do not collapse (CSS 2.1 §8.3.1), so the bottom margin of whatever ended
    /// the document is trapped inside the root's box — <c>body { margin-bottom: 30px }</c> makes
    /// the root thirty pixels taller than anything in it. Measured: a document whose content ends
    /// four pixels short of the sheet and whose root reaches past it prints on ONE page in
    /// Chromium, and used to produce a second, blank one here.
    /// </para>
    /// <para>
    /// A margin is the only thing this drops. An empty box a thousand pixels tall is content and
    /// takes the pages it asks for, which is why the walk is over boxes rather than over ink — and
    /// why a DECLARED height on the root counts, that being the one case where the root's own box
    /// is the deepest thing rather than an artefact of what it contains.
    /// </para>
    /// </remarks>
    static float DocumentHeight(LayoutBox root, List<PageUnit> units)
    {
        var deepest = root.Style.Height.Kind == LengthKind.Absolute ? root.BorderBox.Bottom : 0;

        foreach (var box in root.Descendants())
        {
            if (!ReferenceEquals(box, root))
            {
                deepest = Math.Max(deepest, box.BorderBox.Bottom);
            }
        }

        foreach (var unit in units)
        {
            deepest = Math.Max(deepest, unit.Bounds.Bottom);
        }

        return deepest;
    }

    /// <summary>
    /// Every table header group that is re-drawn when its table continues onto another page, with
    /// the extent over which it does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A browser's printer repeats a <c>thead</c> at the top of every page a table continues onto,
    /// and the rest of the table moves down to make room. That is the one feature people expect
    /// from HTML-to-PDF conversion of a long table, and without it page two of a twenty-row report
    /// is a grid of unlabelled columns.
    /// </para>
    /// <para>
    /// A table may carry more than one header group — <see cref="TableGrid"/> renders them all
    /// first, in source order — so this yields each, and the caller sums their heights. Nested
    /// tables give the outer table's header first, by virtue of the walk order, which is the order
    /// they have to be stacked in on the page.
    /// </para>
    /// <para>
    /// <c>tfoot</c> is the same thing reflected, and is collected by the same walk: a band at the
    /// BOTTOM of a page rather than at the top, drawn where the page's content ended rather than
    /// where it began.
    /// </para>
    /// </remarks>
    static (List<RepeatedRows> Headers, List<RepeatedRows> Footers) RepeatedGroups(LayoutBox root)
    {
        var headers = new List<RepeatedRows>();
        var footers = new List<RepeatedRows>();
        Walk(root);
        return (headers, footers);

        void Walk(LayoutBox box)
        {
            if (box.Style.Display == DisplayKind.Table)
            {
                foreach (var group in box.Children)
                {
                    if (group.BorderBox.Height <= 0)
                    {
                        continue;
                    }

                    if (group.Style.Display == DisplayKind.TableHeaderGroup)
                    {
                        headers.Add(new(group, box, AtFoot: false));
                    }
                    else if (group.Style.Display == DisplayKind.TableFooterGroup)
                    {
                        footers.Add(new(group, box, AtFoot: true));
                    }
                }
            }

            foreach (var child in box.Children)
            {
                Walk(child);
            }

            // A table inside a float or an inline-block is still a table, and the walk that finds
            // its rows for `Unbreakable` reaches it through the same three branches.
            foreach (var floated in box.Floats)
            {
                Walk(floated.Box);
            }

            foreach (var line in box.Lines)
            {
                foreach (var atomic in line.Boxes)
                {
                    Walk(atomic);
                }
            }
        }
    }

    /// <summary>
    /// The headers a page starting at <paramref name="top"/> has to re-draw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A header repeats where the page begins after the header's own top — so the page it was laid
    /// out on draws it in place and no other does — and before the table it belongs to has ended.
    /// </para>
    /// <para>
    /// Bounded at half the page. A header taller than that would turn a continuation page into
    /// mostly header, and a header taller than the page itself would leave nothing for the slice
    /// to advance through, so the page count would grow with the document rather than with its
    /// height. A browser stops repeating for the same reason.
    /// </para>
    /// </remarks>
    static List<RepeatedRows> HeadersAt(List<RepeatedRows> headers, float top, float pageHeight)
    {
        var repeated = new List<RepeatedRows>();
        var height = 0f;

        foreach (var header in headers)
        {
            var band = header.Band;

            if (top <= band.Y ||
                top >= header.TableBottom ||
                height + band.Height > pageHeight / 2)
            {
                continue;
            }

            repeated.Add(header);
            height += band.Height;
        }

        return repeated;
    }

    /// <summary>
    /// The footers a page running from <paramref name="top"/> to <paramref name="end"/> has to
    /// re-draw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The header's rule reflected. A footer repeats where the table has BEGUN by the time the page
    /// ends and the group's own band has not — so the page the group was laid out on draws it in
    /// place and no other does, and a page beginning past the table carries nothing.
    /// </para>
    /// <para>
    /// Bounded at half the page, for the reason the header is: a band taller than that would turn a
    /// continuation page into mostly footer, and one taller than the page would leave nothing for
    /// the slice to advance through.
    /// </para>
    /// </remarks>
    static List<RepeatedRows> FootersAt(
        List<RepeatedRows> footers,
        float top,
        float end,
        float pageHeight)
    {
        var repeated = new List<RepeatedRows>();
        var height = 0f;

        foreach (var footer in footers)
        {
            var band = footer.Band;

            if (footer.TableTop >= end ||
                end >= band.Bottom ||
                footer.TableBottom <= top ||
                height + band.Height > pageHeight / 2)
            {
                continue;
            }

            repeated.Add(footer);
            height += band.Height;
        }

        return repeated;
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
    static (List<(float Position, BreakKind Kind)> Forced, Dictionary<float, float> Avoided) Breaks(
        LayoutBox root)
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

        // A position a page may not begin at, and the position it moves to instead. Not a set:
        // `avoid` names the box the break has to stay clear of, and where the break goes is
        // decided by WHICH side of that box asked. A set would leave the answer to a search
        // through the earlier candidates, and the nearest of those is a line inside the box —
        // which splits the very box the property was written to keep whole.
        var avoided = new Dictionary<float, float>();

        for (var index = 0; index < flow.Count; index++)
        {
            var box = flow[index];
            var style = box.Style;

            if (style.BreakBefore.Forces())
            {
                Add(box.BorderBox.Y, style.BreakBefore);
            }

            // `break-before: avoid` moves the break back to whatever precedes the box in document
            // order — its previous sibling, or its parent when it is a first child, which is the
            // pre-order predecessor in both cases. The box at the very start of the document has
            // none, and a page could not begin earlier than the document does anyway.
            if (style.BreakBefore == BreakKind.Avoid && index > 0)
            {
                Avoid(box.BorderBox.Y, flow[index - 1].BorderBox.Y);
            }

            // Nothing follows the last box in the document, so there is nothing to move onto a
            // page of its own and no break worth taking. A browser emits a trailing blank page
            // here; one blank page at the end of a converted document is the less useful answer.
            if (ends[index] < flow.Count)
            {
                if (style.BreakAfter.Forces())
                {
                    Add(flow[ends[index]].BorderBox.Y, style.BreakAfter);
                }

                // And `break-after: avoid` moves it back to the declaring box's OWN top edge, so
                // the box travels with what follows it. That is the property as every print
                // stylesheet uses it — a heading stranded at the foot of a page is the thing it is
                // written to prevent — and it is why the destination is recorded here rather than
                // searched for later.
                if (style.BreakAfter == BreakKind.Avoid)
                {
                    Avoid(flow[ends[index]].BorderBox.Y, box.BorderBox.Y);
                }
            }
        }

        return ([.. breaks.Select(_ => (_.Key, _.Value))], avoided);

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

        // A FORCED break at the same position wins, which is CSS's own precedence: a break that
        // must be taken beats one that should not be. Recorded first-writer-wins otherwise, so two
        // boxes both avoiding one position do not chase each other.
        void Avoid(float from, float to)
        {
            if (to < from)
            {
                avoided.TryAdd(from, to);
            }
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
            // A row is one by rule, an `avoid` box is one by request, and a replaced element is
            // one because there is nothing inside it to break BETWEEN — but all three are the
            // same thing to the slice: a rectangle that moves whole, rather than a run of lines
            // that move one at a time.
            //
            // The replaced case was invisible until an image grew tall enough to straddle a
            // boundary, which needs one about a page high and no corpus scenario had one. Chrome
            // moves such an image whole to the next page; slicing it instead draws its top on the
            // page before, which `image/svg` caught on its first render. An image taller than a
            // whole page still overflows rather than descending forever, by the same height guard
            // in `NextTop` that a too-tall row relies on.
            if (box.Style.Display == DisplayKind.TableRow ||
                box.Style.BreakInside == BreakKind.Avoid ||
                box.Image is not null)
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
                // A box repeated on every page is on the page it is drawn on by definition, so
                // there is nothing for a break to fall inside and nothing it can lengthen. Left
                // in, a fixed footer near the foot of the page would count as content the
                // document has to make room for and add a page holding nothing else.
                if (positioned.Box.Style.RepeatsOnEveryPage)
                {
                    continue;
                }

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
        Dictionary<float, float> avoided,
        float top,
        float pageHeight,
        bool constrainRuns)
    {
        var chosen = Candidate(units, forced, top, pageHeight, constrainRuns);

        // Where a break asking not to be taken there goes instead. Chained, because the box it
        // moves to may itself be asking — a run of headings each kept with what follows it walks
        // back to the first of them — and bounded by the number of entries, since each step moves
        // strictly earlier and no position repeats.
        //
        // A move that would leave the page holding nothing is refused: `avoid` is a preference and
        // a break has to happen somewhere, so the alternative to taking it here is not taking it
        // at all.
        for (var step = 0; step <= avoided.Count; step++)
        {
            if (!avoided.TryGetValue(chosen, out var earlier) || earlier <= top)
            {
                break;
            }

            chosen = earlier;
        }

        return chosen;
    }

    /// <summary>
    /// Where the page would end with nothing asking otherwise: a forced break if one falls on this
    /// page, else the top of the first unbreakable unit straddling its edge, else the edge itself.
    /// </summary>
    static float Candidate(
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

            // A unit taller than the page is moved to the top of the next one and allowed to
            // overflow from there, which is what a browser does with a picture or a table row that
            // cannot fit on any sheet. Moving it is safe because it starts BELOW this page's top —
            // the loop advances — and a unit already at the top has nowhere better to go, so that
            // one is stepped over and the search continues past it. Without that second half it
            // would move to the top of the next page forever.
            if (bounds.Height > pageHeight && bounds.Y <= top)
            {
                continue;
            }

            if (constrainRuns)
            {
                return Constrained(units, unit, top);
            }

            return bounds.Y;
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

        // Too few ABOVE. Moving the break earlier only takes more lines off the top, so the whole
        // run has to go overleaf or nothing changes — which is what Chromium does with a paragraph
        // whose natural break would strand a single line at the foot of a page.
        //
        // Guarded against a run that starts at or above the page top, where moving to it would not
        // advance and the loop in `Paginate` would never terminate.
        if (above < group.Orphans)
        {
            return group.FirstTop > top ? group.FirstTop : unit.Bounds.Y;
        }

        // Too few BELOW, and enough above. The line that leaves exactly `Widows` below it, when
        // that still leaves `Orphans` above — which needs a run long enough to hold both at once.
        var wanted = group.Count - group.Widows;

        if (wanted >= group.Orphans &&
            Line(units, group, wanted) is {} moved &&
            moved > top)
        {
            return moved;
        }

        // Neither can be met: the run is too short to hold both counts, which under the initial
        // 2 and 2 means any paragraph of three lines. The break stays where it fell and the run
        // splits.
        //
        // MEASURED, and the opposite of what this did first. Moving the whole run overleaf is the
        // tidier-looking answer and is what a print engine is often described as doing; Chromium
        // splits, and `page/break_between_lines` holds the reference — two lines above the break
        // and one below, under the initial `widows: 2` that forbids exactly that. A run this short
        // has no arrangement that satisfies both, so honouring one of them by moving the whole
        // paragraph trades a stranded line for a page ending early, which is not obviously better
        // and is not what the browser does.
        return unit.Bounds.Y;
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
