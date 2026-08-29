/// <summary>
/// Lays out a flex container's children: CSS Flexible Box Layout §9, in main and cross terms.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="TableLayout"/>, this does NOT take over from <see cref="BlockLayout"/>. A flex
/// container's own box is an ordinary block box — it resolves its width the same way, carries the
/// same margins, clamps against the same <c>min-height</c>, establishes the same relative offset
/// and hangs the same list marker — and only the ARRANGEMENT of its children differs. So this
/// replaces one step of <see cref="BlockLayout.Layout"/>, the step that stacks the children, and
/// inherits every other rule already measured against Chrome rather than restating it. A table
/// cannot do that, because a table's own width is a result of sizing its columns.
/// </para>
/// <para>
/// Everything below is written in MAIN and CROSS terms. The axes are mapped once, at the top of
/// <see cref="Layout"/>, and nothing after that asks which <c>flex-direction</c> is in force —
/// which is what lets one implementation carry all four, and what keeps <c>column</c> from being
/// the afterthought it usually is. The mapping back to a rectangle happens in
/// <see cref="Place"/> and nowhere else.
/// </para>
/// <para>
/// Unlike almost everything else in this engine, flexbox IS specified in usable detail, so the
/// rules here are the specification's rather than measurements taken off a browser. The corpus
/// still measures them against Chrome — that is what says the specification was read correctly —
/// but the numbers were not derived from it.
/// </para>
/// </remarks>
static class FlexLayout
{
    /// <summary>
    /// Arranges <paramref name="container"/>'s children and returns the content height they came
    /// to.
    /// </summary>
    /// <param name="container">The flex container. Its children are the flex items.</param>
    /// <param name="contentX">Left edge of the container's content box.</param>
    /// <param name="contentY">Top edge of the container's content box.</param>
    /// <param name="contentWidth">The container's content width, which is already settled.</param>
    /// <param name="fonts">The faces available for measuring text.</param>
    /// <param name="contentHeight">
    /// The container's content height when it has a definite one, and null when it does not. It is
    /// the MAIN size of a column container and the CROSS size of a row one, which is why a column
    /// container with an auto height cannot wrap and a row container with one cannot stretch its
    /// lines.
    /// </param>
    public static float Layout(
        LayoutBox container,
        float contentX,
        float contentY,
        float contentWidth,
        FontSet fonts,
        float? contentHeight)
    {
        var style = container.Style;
        var row = style.FlexDirection is FlexDirectionKind.Row or FlexDirectionKind.RowReverse;

        // A row container's main size is its width, which is always settled by the time this runs;
        // a column container's is its height, which is settled only when something declared one.
        var mainSize = row ? contentWidth : contentHeight;
        var crossSize = row ? contentHeight : contentWidth;

        var mainGap = (row ? style.ColumnGap : style.RowGap).Resolve(mainSize ?? 0);
        var crossGap = (row ? style.RowGap : style.ColumnGap).Resolve(crossSize ?? 0);

        // An absolutely positioned child of a flex container is not a flex item, and its static
        // position is the container's content-box origin. CSS says to place it as though it were
        // the sole flex item — which would run it through the alignment properties — and the two
        // differ only when the container also declares a non-default alignment, which is why the
        // simpler answer is taken and recorded in the todo rather than guessed at.
        foreach (var positioned in container.Positioned)
        {
            positioned.Box.StaticPosition = (contentX, contentY);
        }

        var items = Collect(container, contentWidth, contentHeight, row, fonts);

        if (items.Count == 0)
        {
            return 0;
        }

        var lines = Collect(items, mainSize, mainGap, style.FlexWrap);

        foreach (var line in lines)
        {
            Resolve(line, mainSize ?? Sum(line, mainGap), mainGap);
        }

        // Every item is laid out at the main size the flexing settled on, which is what makes its
        // cross size knowable: a paragraph's height is a function of how wide it was allowed to be.
        foreach (var line in lines)
        {
            foreach (var item in line.Items)
            {
                Measure(item, container, contentWidth, contentHeight, row, fonts);
            }
        }

        SizeLines(lines, crossSize, style);
        Stretch(lines, container, contentWidth, contentHeight, row, fonts);

        var cross = Distribute(lines, crossSize, crossGap, style.AlignContent);

        // What the container's main axis actually spans, which for a definite main size is that
        // size and otherwise is whatever the longest line came to. Both `justify-content` and a
        // reversed direction measure against it, and with no definite size there is no free space
        // for either to work with — so the two answers agree exactly where it matters.
        var main = mainSize ?? Extent(lines, mainGap);

        foreach (var line in lines)
        {
            Justify(line, main, mainGap, style.JustifyContent);
            Align(line, style, row);
        }

        Place(lines, contentX, contentY, main, cross, row, style);

        return row ? cross : main;
    }

    /// <summary>
    /// The min-content and max-content MAIN sizes of a flex container's content, for a caller that
    /// has to size the container before laying it out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A shrink-to-fit float, an inline-flex on a line and a table cell all need this. Both answers
    /// are the standard approximations rather than a full pass of the algorithm: a container's
    /// max-content main size is the sum of its items' outer max-content contributions, because
    /// given unlimited room nothing wraps and nothing shrinks; its min-content size is that same
    /// sum for a <c>nowrap</c> container, and for a wrapping one the largest single item, because
    /// every line may hold one item.
    /// </para>
    /// <para>
    /// A COLUMN container reverses the two: its width is the cross axis, so the widest item decides
    /// both, and the sum decides nothing at all.
    /// </para>
    /// </remarks>
    public static (float Min, float Max) Intrinsic(LayoutBox container, FontSet fonts)
    {
        var style = container.Style;
        var row = style.FlexDirection is FlexDirectionKind.Row or FlexDirectionKind.RowReverse;

        var minSum = 0f;
        var maxSum = 0f;
        var minLargest = 0f;
        var maxLargest = 0f;
        var count = 0;

        foreach (var child in container.Children)
        {
            var (childMin, childMax) = IntrinsicWidths.Measure(child, fonts);

            // Percentages resolve to zero in an intrinsic pass, for the reason
            // `IntrinsicWidths` gives: the containing block they would resolve against is what is
            // being computed.
            var margins =
                child.Style.MarginLeft.Resolve(0) +
                child.Style.MarginRight.Resolve(0);

            minSum += childMin + margins;
            maxSum += childMax + margins;
            minLargest = Math.Max(minLargest, childMin + margins);
            maxLargest = Math.Max(maxLargest, childMax + margins);
            count++;
        }

        if (!row)
        {
            return (minLargest, maxLargest);
        }

        var gaps = count > 1 ? (count - 1) * style.ColumnGap.Resolve(0) : 0;

        return (
            style.FlexWrap == FlexWrapKind.NoWrap ? minSum + gaps : minLargest,
            maxSum + gaps);
    }

    /// <summary>
    /// The container's children as flex items, in <c>order</c> order, each with its base size and
    /// its bounds settled.
    /// </summary>
    /// <remarks>
    /// The sort is STABLE, because <c>order</c> ties fall back to document order — the same
    /// requirement <c>z-index</c> carries, and unstable is correct on every arrangement where no
    /// two items share a value, which is most of them. <c>List.Sort</c> is not stable, so this
    /// goes through <c>OrderBy</c>, which is.
    /// </remarks>
    static List<FlexItem> Collect(
        LayoutBox container,
        float contentWidth,
        float? contentHeight,
        bool row,
        FontSet fonts)
    {
        var items = new List<FlexItem>();

        foreach (var box in container.Children.OrderBy(_ => _.Style.Order))
        {
            var item = new FlexItem
            {
                Box = box
            };

            var style = box.Style;

            // Margins resolve against the container's INLINE size on every side, vertical ones
            // included — CSS 2.1 §8.3's rule, which flexbox does not change.
            var top = style.MarginTop.ResolveOrNull(contentWidth);
            var right = style.MarginRight.ResolveOrNull(contentWidth);
            var bottom = style.MarginBottom.ResolveOrNull(contentWidth);
            var left = style.MarginLeft.ResolveOrNull(contentWidth);

            (item.MainStart, item.MainEnd, item.CrossStart, item.CrossEnd) = row
                ? (left ?? 0, right ?? 0, top ?? 0, bottom ?? 0)
                : (top ?? 0, bottom ?? 0, left ?? 0, right ?? 0);

            (item.AutoMainStart, item.AutoMainEnd, item.AutoCrossStart, item.AutoCrossEnd) = row
                ? (left is null, right is null, top is null, bottom is null)
                : (top is null, bottom is null, left is null, right is null);

            var surroundX = style.SurroundX(contentWidth);
            var surroundY = style.SurroundY(contentWidth);
            (item.MainSurround, item.CrossSurround) = row
                ? (surroundX, surroundY)
                : (surroundY, surroundX);

            Size(item, container, contentWidth, contentHeight, row, fonts);

            items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// Settles one item's flex base size and the bounds the flexing may not take it outside of.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CSS Flexbox §9.2. <c>flex-basis</c> decides the base size; where it defers to the main size
    /// property and that is auto as well, the item's MAX-CONTENT size is the basis — which is what
    /// makes a row of unstyled items size to their words before any growing or shrinking happens.
    /// </para>
    /// <para>
    /// The awkward half is a COLUMN container, whose main axis is the block axis: a max-content
    /// height is not a measurement, it is a layout, and it cannot be taken until the item's WIDTH
    /// is known. So the cross size is settled first there and the item is laid out once to find
    /// its natural height. A row container needs no such pass, <see cref="IntrinsicWidths"/>
    /// answering for it without laying anything out.
    /// </para>
    /// </remarks>
    static void Size(
        FlexItem item,
        LayoutBox container,
        float contentWidth,
        float? contentHeight,
        bool row,
        FontSet fonts)
    {
        var style = item.Style;
        var basis = style.FlexBasis;
        var mainProperty = row ? style.Width : style.Height;

        // What a percentage along the main axis resolves against, and null when the container has
        // no definite main size — at which point a percentage basis behaves as `content`, exactly
        // as a percentage height does against an indefinite containing height.
        var mainBasis = row ? contentWidth : contentHeight;

        float? declared = null;

        if (!basis.IsAuto && !basis.IsNone)
        {
            // `flex-basis: content` reaches here as an unparseable value and so as `auto`, which
            // is the same answer by a different route: both fall through to the max-content size
            // below when the main size property is auto too.
            declared = Definite(basis, mainBasis, style, item.MainSurround);
        }

        declared ??= Definite(mainProperty, mainBasis, style, item.MainSurround);

        // A COLUMN container's items are sized across before they are sized along, and this has to
        // happen whether or not the main size turns out to be declared: the cross size is the
        // WIDTH there, and every later step needs it — the layout that finds a natural height,
        // the re-layout at the flexed height, and the placement. Settling it only on the branch
        // that needs it to measure a height leaves every item with a declared height at a width of
        // zero, which is a whole column of invisible boxes in the one arrangement most likely to
        // be written.
        if (!row)
        {
            item.Cross = CrossOf(item, container, contentWidth, fonts);
        }

        if (declared is {} given)
        {
            item.Base = given + item.MainSurround;
        }
        else if (row)
        {
            item.Base = IntrinsicWidths.Measure(item.Box, fonts).Max;
        }
        else
        {
            item.Base = BlockLayout.Layout(
                item.Box,
                0,
                0,
                contentWidth,
                fonts,
                item.Cross,
                containingHeight: contentHeight);
        }

        item.MinMain = Minimum(item, container, contentWidth, contentHeight, row, fonts);

        if (Definite(row ? style.MaxWidth : style.MaxHeight, mainBasis, style, item.MainSurround) is {} max)
        {
            item.MaxMain = max + item.MainSurround;
        }

        item.Hypothetical = Math.Clamp(item.Base, item.MinMain, Math.Max(item.MinMain, item.MaxMain));
    }

    /// <summary>
    /// The smallest main size an item may be flexed to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CSS Flexbox §4.5, the automatic minimum size, and the rule that makes <c>flex-shrink</c>
    /// usable at all: an item whose minimum is <c>auto</c> — which is the initial value, so every
    /// item that does not say otherwise — will not shrink below the smaller of what it asked for
    /// and what its content needs. Without it a row of paragraphs squeezed by a narrow container
    /// collapses to nothing, because the shrink factor has no floor to stop at.
    /// </para>
    /// <para>
    /// The two halves are the specification's own: the CONTENT size suggestion is the item's
    /// min-content size, and the SPECIFIED size suggestion is its declared main size where it has
    /// one. An item that declares a size smaller than its longest word takes the declaration,
    /// which is what keeps the rule from making a deliberately narrow item wide again.
    /// </para>
    /// <para>
    /// A declared <c>min-width</c> or <c>min-height</c> replaces the whole of this, being an
    /// explicit answer to the same question.
    /// </para>
    /// </remarks>
    static float Minimum(
        FlexItem item,
        LayoutBox container,
        float contentWidth,
        float? contentHeight,
        bool row,
        FontSet fonts)
    {
        var style = item.Style;
        var property = row ? style.MinWidth : style.MinHeight;
        var mainBasis = row ? contentWidth : contentHeight;

        if (Definite(property, mainBasis, style, item.MainSurround) is {} declared)
        {
            return declared + item.MainSurround;
        }

        if (!property.IsAuto)
        {
            // A percentage against nothing to resolve it against, which behaves as though the
            // property were not there — and for a MINIMUM that means zero rather than the
            // content-based floor, since the author did name a value.
            return 0;
        }

        var content = row
            ? IntrinsicWidths.ContentMinimum(item.Box, fonts)
            : NaturalHeight(item, container, contentWidth, contentHeight, fonts);

        var specified = Definite(
            row ? style.Width : style.Height,
            mainBasis,
            style,
            item.MainSurround);

        var minimum = specified is {} given
            ? Math.Min(given + item.MainSurround, content)
            : content;

        // Bounded by the item's own maximum, which CSS asks for explicitly: a content-based floor
        // above a declared ceiling would make the ceiling unreachable.
        if (Definite(row ? style.MaxWidth : style.MaxHeight, mainBasis, style, item.MainSurround) is {} max)
        {
            minimum = Math.Min(minimum, max + item.MainSurround);
        }

        return minimum;
    }

    /// <summary>
    /// The height an item comes to at its cross size, for a column container's content-based
    /// minimum.
    /// </summary>
    /// <remarks>
    /// The item has already been laid out once by <see cref="Size"/> to find its base size, and
    /// this is that same height, re-deriving it not being free. Which means an item whose height
    /// is DECLARED gets its declaration back rather than the min-content height CSS asks for — so
    /// the specified size suggestion wins outright there, and such an item will not shrink below
    /// its declared height even where its content would allow it. Recorded in the todo; measuring
    /// the other answer needs a second layout with the declaration suppressed.
    /// </remarks>
    static float NaturalHeight(
        FlexItem item,
        LayoutBox container,
        float contentWidth,
        float? contentHeight,
        FontSet fonts)
    {
        if (item.Base > 0)
        {
            return item.Base;
        }

        return BlockLayout.Layout(
            item.Box,
            0,
            0,
            contentWidth,
            fonts,
            item.Cross > 0 ? item.Cross : CrossOf(item, container, contentWidth, fonts),
            containingHeight: contentHeight);
    }

    /// <summary>
    /// The cross (horizontal) size a COLUMN container's item takes: the container's own content
    /// width when it stretches, and its shrink-to-fit width otherwise.
    /// </summary>
    static float CrossOf(FlexItem item, LayoutBox container, float contentWidth, FontSet fonts)
    {
        var available = Math.Max(0, contentWidth - item.CrossStart - item.CrossEnd);

        if (item.Style.Width.ResolveOrNull(contentWidth) is {} width)
        {
            var surround = item.Style.SurroundX(contentWidth);

            return item.Style.ContentSize(width, surround) + surround;
        }

        if (Alignment(item, container.Style) == AlignKind.Stretch &&
            !item.AutoCrossStart &&
            !item.AutoCrossEnd)
        {
            return available;
        }

        var (min, max) = IntrinsicWidths.Measure(item.Box, fonts);

        return Math.Min(Math.Max(min, available), max);
    }

    /// <summary>
    /// One of the size properties as a CONTENT size, or null when it settles nothing.
    /// </summary>
    /// <remarks>
    /// The same shape as <see cref="BlockLayout"/>'s own, and for the same reason: a percentage
    /// against an indefinite basis behaves as though the property were absent, and
    /// <c>box-sizing</c> applies to <c>flex-basis</c> exactly as it does to <c>width</c>.
    /// </remarks>
    static float? Definite(CssLength length, float? basis, ComputedStyle style, float surround) =>
        length.Kind switch
        {
            LengthKind.Absolute => style.ContentSize(length.Value, surround),
            LengthKind.Percent or LengthKind.Calc when basis is {} definite =>
                style.ContentSize(Math.Max(0, length.Resolve(definite)), surround),
            _ => null
        };

    /// <summary>
    /// Gathers the items into flex lines.
    /// </summary>
    /// <remarks>
    /// Greedy, which is what CSS asks for: an item goes on the current line unless it and
    /// everything already there would not fit, and a line always holds at least one item however
    /// far it overflows. A container with no definite main size cannot wrap — there is no width to
    /// measure against — which is why an auto-height column container comes out as one line
    /// whatever <c>flex-wrap</c> says.
    /// </remarks>
    static List<FlexLine> Collect(List<FlexItem> items, float? mainSize, float gap, FlexWrapKind wrap)
    {
        if (wrap == FlexWrapKind.NoWrap || mainSize is not {} available)
        {
            return [new() {Items = items}];
        }

        var lines = new List<FlexLine>();
        var current = new List<FlexItem>();
        var used = 0f;

        foreach (var item in items)
        {
            var needed = item.OuterHypothetical + (current.Count > 0 ? gap : 0);

            if (current.Count > 0 && used + needed > available)
            {
                lines.Add(new() {Items = current});
                current = [];
                used = 0;
                needed = item.OuterHypothetical;
            }

            current.Add(item);
            used += needed;
        }

        lines.Add(new() {Items = current});

        return lines;
    }

    /// <summary>The main-axis room one line's items ask for, gaps included.</summary>
    static float Sum(FlexLine line, float gap) =>
        line.Items.Sum(_ => _.OuterHypothetical) + Gaps(line.Items.Count, gap);

    /// <summary>The main-axis room one line's items ended up taking.</summary>
    static float Used(FlexLine line, float gap) =>
        line.Items.Sum(_ => _.OuterMain) + Gaps(line.Items.Count, gap);

    /// <summary>A run of <paramref name="count"/> items has one fewer gap between them.</summary>
    static float Gaps(int count, float gap) =>
        count > 1 ? (count - 1) * gap : 0;

    /// <summary>
    /// Resolves the flexible lengths on one line: CSS Flexbox §9.7.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The loop is the part worth reading. Distributing the free space in proportion to the flex
    /// factors is the easy half and is wrong on its own, because an item may be clamped by its own
    /// minimum or maximum — and the space it could not take has to go back to the others. So each
    /// pass distributes, clamps, and then FREEZES whichever items were clamped in the direction the
    /// total violation went, leaving the rest to share what is left. It terminates because every
    /// pass freezes at least one item.
    /// </para>
    /// <para>
    /// The two directions are not symmetric. Growing shares the space in proportion to the grow
    /// factors alone; shrinking scales each factor by the item's own base size first, so a wide
    /// item gives up more than a narrow one with the same <c>flex-shrink</c>. Without that scaling
    /// a row of differently sized items squeezed by a narrow container loses the same number of
    /// pixels from each, and the small ones vanish first.
    /// </para>
    /// </remarks>
    static void Resolve(FlexLine line, float available, float gap)
    {
        var items = line.Items;
        var content = Math.Max(0, available - Gaps(items.Count, gap));
        var grow = items.Sum(_ => _.OuterHypothetical) <= content;

        foreach (var item in items)
        {
            var factor = grow ? item.Style.FlexGrow : item.Style.FlexShrink;

            // An item that cannot flex, or one whose base size is already on the wrong side of its
            // hypothetical size, is settled before the loop starts.
            item.Frozen =
                factor == 0 ||
                (grow && item.Base > item.Hypothetical) ||
                (!grow && item.Base < item.Hypothetical);

            item.Main = item.Frozen ? item.Hypothetical : item.Base;
        }

        // The free space as it stood before any flexing, which bounds how much the loop may hand
        // out when the factors do not sum to one — CSS's rule for `flex: 0.5`, which takes half
        // the free space rather than all of it.
        var initial = content - items.Sum(_ => _.MainStart + (_.Frozen ? _.Main : _.Base) + _.MainEnd);

        for (var pass = 0; pass <= items.Count && items.Any(_ => !_.Frozen); pass++)
        {
            var unfrozen = items.Where(_ => !_.Frozen).ToList();

            // A FROZEN item counts at the size it was frozen at and an unfrozen one at its BASE
            // size, never at whatever the previous pass happened to clamp it to. Counting the
            // clamped size instead makes the space a clamp released invisible — the pass that
            // freed sixty pixels by capping one item then has sixty to share rather than the four
            // hundred and sixty that are really left, and every item after the first clamp comes
            // out too small.
            var remaining = content - items.Sum(_ =>
                _.MainStart + (_.Frozen ? _.Main : _.Base) + _.MainEnd);

            var total = grow
                ? unfrozen.Sum(_ => _.Style.FlexGrow)
                : unfrozen.Sum(_ => _.Style.FlexShrink * _.InnerBase);

            if (total <= 0)
            {
                break;
            }

            // A grow factor summing below one takes only that fraction of the free space, and a
            // scaled shrink factor summing below one gives up only that fraction of the shortfall.
            if (grow && unfrozen.Sum(_ => _.Style.FlexGrow) < 1)
            {
                remaining = Math.Min(remaining, initial * unfrozen.Sum(_ => _.Style.FlexGrow));
            }
            else if (!grow && unfrozen.Sum(_ => _.Style.FlexShrink) < 1)
            {
                remaining = Math.Max(remaining, initial * unfrozen.Sum(_ => _.Style.FlexShrink));
            }

            var violation = 0f;

            // Per item, and that is the whole point: the freeze below is decided by whether THIS
            // item's own clamp moved it, not by how its size compares to anything else. Reading
            // the total's sign against each item's size instead freezes items the clamp never
            // touched, and the loop then hands the recovered space to nobody.
            var clamps = new float[unfrozen.Count];

            for (var index = 0; index < unfrozen.Count; index++)
            {
                var item = unfrozen[index];

                var share = grow
                    ? item.Style.FlexGrow / total
                    : item.Style.FlexShrink * item.InnerBase / total;

                var unclamped = item.Base + remaining * share;
                var clamped = Math.Clamp(unclamped, item.MinMain, Math.Max(item.MinMain, item.MaxMain));

                clamps[index] = clamped - unclamped;
                violation += clamps[index];
                item.Main = clamped;
            }

            for (var index = 0; index < unfrozen.Count; index++)
            {
                // Nothing was clamped anywhere, so every item took its share and the line is
                // settled. A positive total means minimums won somewhere, and the items whose own
                // minimum won are frozen at it while the rest go round again with less to share; a
                // negative total is the same thing reflected through the maximums.
                unfrozen[index].Frozen =
                    violation == 0 ||
                    (violation > 0 && clamps[index] > Epsilon) ||
                    (violation < 0 && clamps[index] < -Epsilon);
            }
        }

        foreach (var item in items)
        {
            // Whatever the loop left unfrozen — which only a pathological set of factors can
            // reach — is settled at the size it reached rather than at nothing.
            item.Main = Math.Max(0, item.Main);
            item.Frozen = true;
        }
    }

    /// <summary>
    /// How far two lengths may differ and still count as the same, when deciding which items a
    /// flexing pass clamped.
    /// </summary>
    /// <remarks>
    /// The comparison is between a distributed share and a base size, both of which have been
    /// through a multiply and a divide, so exact equality would leave an item unfrozen forever on
    /// a rounding difference. A thousandth of a pixel is far below anything the corpus can see and
    /// far above what the arithmetic produces.
    /// </remarks>
    const float Epsilon = 0.001f;

    /// <summary>
    /// Lays one item out at the main size the flexing gave it, and records the cross size it comes
    /// to.
    /// </summary>
    static void Measure(
        FlexItem item,
        LayoutBox container,
        float contentWidth,
        float? contentHeight,
        bool row,
        FontSet fonts)
    {
        if (row)
        {
            item.Cross = BlockLayout.Layout(
                item.Box,
                0,
                0,
                contentWidth,
                fonts,
                item.Main,
                containingHeight: contentHeight);

            return;
        }

        // A column container settled the cross size before the main one, the height having
        // depended on it — so this is a re-layout at the height the flexing chose rather than a
        // first measurement.
        BlockLayout.Layout(
            item.Box,
            0,
            0,
            contentWidth,
            fonts,
            item.Cross,
            assignedHeight: Math.Max(0, item.Main - item.MainSurround),
            containingHeight: contentHeight);

        item.Cross = item.Box.BorderBox.Width;
    }

    /// <summary>
    /// Gives each line its cross size: CSS Flexbox §9.4.
    /// </summary>
    /// <remarks>
    /// A single line in a container with a definite cross size takes the whole of it, which is what
    /// makes <c>align-items: center</c> centre an item in a container that declared a height and do
    /// nothing at all in one that did not. Every other line takes the largest thing on it — with
    /// the baseline-aligned items measured as two quantities rather than one, since what they need
    /// is the deepest baseline plus the furthest descent below any baseline, and no single item
    /// accounts for that sum. The same rule a table row's height follows, reached from a different
    /// specification.
    /// </remarks>
    static void SizeLines(List<FlexLine> lines, float? crossSize, ComputedStyle style)
    {
        var row = style.FlexDirection is FlexDirectionKind.Row or FlexDirectionKind.RowReverse;

        foreach (var line in lines)
        {
            line.Baseline = Baselines(line, style, row);

            var extent = line.Baseline;

            foreach (var item in line.Items)
            {
                extent = Math.Max(
                    extent,
                    Aligns(item, style, row) == AlignKind.Baseline
                        ? line.Baseline + item.OuterCross - item.Baseline
                        : item.OuterCross);
            }

            line.Cross = extent;
        }

        // A container that cannot wrap has exactly one line, and that line IS the container's
        // cross axis — so a definite cross size belongs to it whole. This is what makes
        // `align-items: center` centre an item in a container that declared a height and do
        // nothing at all in one that did not, which is the single most confusing thing about the
        // property until the reason is stated.
        if (style.FlexWrap == FlexWrapKind.NoWrap && crossSize is {} definite && lines.Count == 1)
        {
            lines[0].Cross = definite;
        }
    }

    /// <summary>
    /// Where a line puts the baseline its baseline-aligned items share, measured from the line's
    /// cross-start edge.
    /// </summary>
    /// <remarks>
    /// An item with no line box of its own has a baseline SYNTHESISED from its border box, which
    /// CSS Flexbox §8.3 asks for by name — so an empty item, or one holding nothing but a picture,
    /// aligns by its bottom border edge rather than dropping out of the group.
    /// </remarks>
    static float Baselines(FlexLine line, ComputedStyle style, bool row)
    {
        var deepest = 0f;

        foreach (var item in line.Items)
        {
            if (Aligns(item, style, row) != AlignKind.Baseline)
            {
                continue;
            }

            item.Baseline = item.CrossStart + (FirstBaseline(item.Box) ?? item.Box.BorderBox.Height);
            deepest = Math.Max(deepest, item.Baseline);
        }

        return deepest;
    }

    /// <summary>
    /// Where a box's FIRST line puts its baseline, measured from its border-box top, or null when
    /// it has no line to take one from.
    /// </summary>
    /// <remarks>
    /// The same walk <see cref="TableLayout"/> makes for a cell, and for the same reason: an item
    /// whose first line is several blocks down still aligns on it.
    /// </remarks>
    static float? FirstBaseline(LayoutBox box)
    {
        foreach (var descendant in box.Descendants())
        {
            if (descendant.Lines.Count > 0)
            {
                return descendant.Lines[0].Bounds.Y + descendant.Lines[0].Baseline - box.BorderBox.Y;
            }
        }

        return null;
    }

    /// <summary>
    /// Stretches the items whose cross size is auto to fill their line: CSS Flexbox §9.4 step 11.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what makes a row of cards in a flex container come out the same height however much
    /// text each holds, and it is why <c>align-items: stretch</c> is the initial value rather than
    /// <c>flex-start</c>. Only an item that declared no cross size stretches — a declared height
    /// is an answer already given — and an item with an auto margin on the cross axis does not
    /// either, that margin having asked for the leftover space instead.
    /// </para>
    /// <para>
    /// A row container re-lays the item out with an assigned HEIGHT, which is the one place
    /// anything but <see cref="AbsoluteLayout"/> uses that parameter. A column container assigns a
    /// width instead, which is the ordinary path.
    /// </para>
    /// </remarks>
    static void Stretch(
        List<FlexLine> lines,
        LayoutBox container,
        float contentWidth,
        float? contentHeight,
        bool row,
        FontSet fonts)
    {
        var style = container.Style;

        foreach (var line in lines)
        {
            foreach (var item in line.Items)
            {
                if (Aligns(item, style, row) != AlignKind.Stretch ||
                    item.AutoCrossStart ||
                    item.AutoCrossEnd)
                {
                    continue;
                }

                var declared = row ? item.Style.Height : item.Style.Width;

                if (!declared.IsAuto)
                {
                    continue;
                }

                var stretched = Math.Max(0, line.Cross - item.CrossStart - item.CrossEnd);

                if (Math.Abs(stretched - item.Cross) < Epsilon)
                {
                    continue;
                }

                // Clamped by the item's own maximum and minimum on the cross axis, which stretching
                // may not override — CSS Flexbox §9.4 says so, and without it `max-height` on a
                // card in a row of taller cards reaches nothing.
                stretched = ClampCross(item, stretched, contentWidth, contentHeight, row);

                if (row)
                {
                    BlockLayout.Layout(
                        item.Box,
                        0,
                        0,
                        contentWidth,
                        fonts,
                        item.Main,
                        assignedHeight: Math.Max(0, stretched - item.CrossSurround),
                        containingHeight: contentHeight);
                }
                else
                {
                    BlockLayout.Layout(
                        item.Box,
                        0,
                        0,
                        contentWidth,
                        fonts,
                        stretched,
                        assignedHeight: Math.Max(0, item.Main - item.MainSurround),
                        containingHeight: contentHeight);
                }

                item.Cross = stretched;
            }
        }
    }

    /// <summary>Applies the item's cross-axis minimum and maximum to a stretched size.</summary>
    static float ClampCross(
        FlexItem item,
        float size,
        float contentWidth,
        float? contentHeight,
        bool row)
    {
        var style = item.Style;
        var basis = row ? contentHeight : contentWidth;

        if (Definite(row ? style.MaxHeight : style.MaxWidth, basis, style, item.CrossSurround) is {} max)
        {
            size = Math.Min(size, max + item.CrossSurround);
        }

        if (Definite(row ? style.MinHeight : style.MinWidth, basis, style, item.CrossSurround) is {} min)
        {
            size = Math.Max(size, min + item.CrossSurround);
        }

        return size;
    }

    /// <summary>
    /// Positions the lines along the cross axis and returns the cross size the container came to.
    /// </summary>
    /// <remarks>
    /// <c>align-content</c> reaches nothing on a single-line container, which is CSS's own rule and
    /// the one thing about the property that surprises people: centring a lone line is
    /// <c>align-items</c>'s business, this being about the space BETWEEN lines. It also reaches
    /// nothing where the container has no definite cross size, there being no leftover to
    /// distribute.
    /// </remarks>
    static float Distribute(
        List<FlexLine> lines,
        float? crossSize,
        float gap,
        ContentDistributionKind align)
    {
        var content = lines.Sum(_ => _.Cross) + Gaps(lines.Count, gap);

        // Nothing to distribute without a cross size to distribute inside, so the lines simply
        // stack and the container takes what they came to.
        if (crossSize is not {} available)
        {
            var stacked = 0f;

            foreach (var line in lines)
            {
                line.CrossPosition = stacked;
                stacked += line.Cross + gap;
            }

            return content;
        }

        var free = available - content;

        if (align == ContentDistributionKind.Stretch && free > 0)
        {
            var share = free / lines.Count;

            foreach (var line in lines)
            {
                line.Cross += share;
            }

            free = 0;
        }

        var (offset, between) = Positions(align, free, lines.Count, gap);

        foreach (var line in lines)
        {
            line.CrossPosition = offset;
            offset += line.Cross + between;
        }

        return available;
    }

    /// <summary>
    /// Where the first thing goes and how far apart the rest are, for one distribution.
    /// </summary>
    /// <remarks>
    /// One method for <c>justify-content</c> and <c>align-content</c> alike, the two being the same
    /// arithmetic on different axes. The three <c>space-*</c> values differ only in what happens at
    /// the EDGES — none, half a share, a whole share — which is the whole of what separates them
    /// and is easy to get subtly wrong when each is written out on its own.
    /// </remarks>
    static (float Offset, float Between) Positions(
        ContentDistributionKind align,
        float free,
        int count,
        float gap)
    {
        // Negative free space is an overflow, and every distribution puts it at the end rather
        // than sharing it out: `space-between` on an overflowing line packs to the start, which is
        // what a browser does and what keeps the first item visible.
        if (free < 0 || count == 0)
        {
            return align switch
            {
                ContentDistributionKind.End => (free, gap),
                ContentDistributionKind.Center => (free / 2, gap),
                _ => (0, gap)
            };
        }

        return align switch
        {
            ContentDistributionKind.End => (free, gap),
            ContentDistributionKind.Center => (free / 2, gap),
            ContentDistributionKind.SpaceBetween when count > 1 => (0, gap + free / (count - 1)),
            ContentDistributionKind.SpaceAround => (free / count / 2, gap + free / count),
            ContentDistributionKind.SpaceEvenly => (free / (count + 1), gap + free / (count + 1)),
            _ => (0, gap)
        };
    }

    /// <summary>
    /// Positions one line's items along the main axis: auto margins first, then
    /// <c>justify-content</c>.
    /// </summary>
    /// <remarks>
    /// The order is CSS's and it matters: an auto margin absorbs ALL the free space before
    /// <c>justify-content</c> is consulted, so a container declaring <c>space-between</c> and an
    /// item declaring <c>margin-left: auto</c> honours the margin and packs everything else to the
    /// start. Which is why the two are so often written together by mistake.
    /// </remarks>
    static void Justify(FlexLine line, float available, float gap, ContentDistributionKind justify)
    {
        var free = available - Used(line, gap);
        var autos = line.Items.Sum(_ => (_.AutoMainStart ? 1 : 0) + (_.AutoMainEnd ? 1 : 0));

        if (autos > 0 && free > 0)
        {
            var share = free / autos;
            var position = 0f;

            foreach (var item in line.Items)
            {
                if (item.AutoMainStart)
                {
                    item.MainStart = share;
                }

                if (item.AutoMainEnd)
                {
                    item.MainEnd = share;
                }

                item.MainPosition = position + item.MainStart;
                position = item.MainPosition + item.Main + item.MainEnd + gap;
            }

            return;
        }

        var (offset, between) = Positions(justify, free, line.Items.Count, gap);

        foreach (var item in line.Items)
        {
            item.MainPosition = offset + item.MainStart;
            offset = item.MainPosition + item.Main + item.MainEnd + between;
        }
    }

    /// <summary>Positions one line's items across it, per <c>align-self</c>.</summary>
    /// <remarks>
    /// An auto margin on the cross axis takes precedence here as it does on the main axis, and it
    /// is the reason a lone <c>margin-top: auto</c> pushes an item to the bottom of its line
    /// whatever the container's <c>align-items</c> says.
    /// </remarks>
    static void Align(FlexLine line, ComputedStyle style, bool row)
    {
        foreach (var item in line.Items)
        {
            var free = line.Cross - item.OuterCross;

            if (item.AutoCrossStart || item.AutoCrossEnd)
            {
                var autos = (item.AutoCrossStart ? 1 : 0) + (item.AutoCrossEnd ? 1 : 0);
                var share = Math.Max(0, free) / autos;

                if (item.AutoCrossStart)
                {
                    item.CrossStart = share;
                }

                if (item.AutoCrossEnd)
                {
                    item.CrossEnd = share;
                }

                item.CrossPosition = item.CrossStart;

                continue;
            }

            item.CrossPosition = Aligns(item, style, row) switch
            {
                AlignKind.End => Math.Max(0, free) + item.CrossStart,
                AlignKind.Center => free / 2 + item.CrossStart,
                AlignKind.Baseline => line.Baseline - item.Baseline + item.CrossStart,
                _ => item.CrossStart
            };
        }
    }

    /// <summary>
    /// How an item aligns on the cross axis, with <c>align-self: auto</c> resolved against its
    /// container.
    /// </summary>
    static AlignKind Alignment(FlexItem item, ComputedStyle container) =>
        item.Style.AlignSelf == AlignKind.Auto ? container.AlignItems : item.Style.AlignSelf;

    /// <summary>
    /// The same, with the values that only mean something on one axis folded away.
    /// </summary>
    /// <remarks>
    /// <c>baseline</c> needs the cross axis to be the BLOCK axis, a baseline being a horizontal
    /// line — so in a column container, where the cross axis runs across the page, it behaves as
    /// <c>flex-start</c>. That is CSS's own fallback rather than a simplification.
    /// </remarks>
    static AlignKind Aligns(FlexItem item, ComputedStyle container, bool row)
    {
        var align = Alignment(item, container);

        if (align == AlignKind.Baseline && !row)
        {
            return AlignKind.Start;
        }

        return align;
    }

    /// <summary>The main-axis extent the lines came to, for a container sized by its content.</summary>
    static float Extent(List<FlexLine> lines, float gap) =>
        lines.Count == 0 ? 0 : lines.Max(_ => Used(_, gap));

    /// <summary>
    /// Moves every item to where the algorithm put it, mapping main and cross back onto x and y.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one place the axes are unmapped, and the one place the REVERSE directions mean anything:
    /// everything above packs from a start edge without knowing which edge that is, and a reversed
    /// axis is a subtraction here. <c>wrap-reverse</c> is the same subtraction on the cross axis,
    /// which is why the two compose without either knowing about the other.
    /// </para>
    /// <para>
    /// The translate is ABSOLUTE rather than relative to where the box currently sits. Every item
    /// was laid out with an assigned main size, so its border box starts at the origin — except
    /// where the item is relatively positioned, in which case <see cref="BlockLayout"/> has already
    /// shifted it and translating by the difference would cancel exactly the offset the property
    /// asked for.
    /// </para>
    /// </remarks>
    static void Place(
        List<FlexLine> lines,
        float contentX,
        float contentY,
        float mainSize,
        float crossSize,
        bool row,
        ComputedStyle style)
    {
        var reverseMain = style.FlexDirection is FlexDirectionKind.RowReverse or FlexDirectionKind.ColumnReverse;
        var reverseCross = style.FlexWrap == FlexWrapKind.WrapReverse;

        foreach (var line in lines)
        {
            var crossPosition = reverseCross
                ? crossSize - line.CrossPosition - line.Cross
                : line.CrossPosition;

            foreach (var item in line.Items)
            {
                var main = reverseMain
                    ? mainSize - item.MainPosition - item.Main
                    : item.MainPosition;

                // A reversed cross axis flips the line's band and not the item inside it, so an
                // item aligned to the line's start stays at the line's start — which under
                // `wrap-reverse` is the edge nearer the bottom of the page.
                var cross = reverseCross
                    ? line.Cross - item.CrossPosition - item.Cross
                    : item.CrossPosition;

                var (x, y) = row
                    ? (contentX + main, contentY + crossPosition + cross)
                    : (contentX + crossPosition + cross, contentY + main);

                item.Box.Translate(x, y);
            }
        }
    }
}
