/// <summary>
/// Lays out a block formatting context: block boxes stacked vertically, with collapsing margins.
/// </summary>
/// <remarks>
/// <para>
/// The contract every method here shares is that <b>a box never applies its own vertical
/// margins</b>. <see cref="Layout"/> is handed the top of the border box and positions from there;
/// whoever calls it is responsible for having consulted <see cref="LeadingMargin"/> first. That
/// split exists because a margin does not belong to one box — it collapses through nesting, so the
/// margin above a box may have come from a grandchild, and only the ancestor placing the box can
/// know the final value.
/// </para>
/// <para>
/// Floats take part, through a <see cref="FloatContext"/> threaded down the tree. A float is
/// removed from the stacking that this class does — it neither advances the flow position nor
/// counts toward the height — and reappears only in the band <see cref="InlineLayout"/> asks for
/// when placing each line. Block boxes beside a float are deliberately NOT narrowed or moved:
/// CSS gives them their full width and lets them overlap, and only the line boxes inside them
/// wrap.
/// </para>
/// <para>
/// Positioned boxes: a relative one is laid out in flow and shifted at the end of
/// <see cref="Layout"/>, taking its subtree with it and leaving every measurement made against it
/// untouched. An absolute one is skipped by flow entirely — this class only records where flow
/// WOULD have put it, since that is where it goes when its offsets are auto — and
/// <see cref="AbsoluteLayout"/> places it afterwards.
/// </para>
/// </remarks>
static class BlockLayout
{
    /// <summary>
    /// Lays out <paramref name="box"/> with its border box starting at
    /// (<paramref name="x"/>, <paramref name="y"/>), and returns the border box height.
    /// </summary>
    /// <param name="box">The box to lay out.</param>
    /// <param name="x">Left edge of the containing block's content box.</param>
    /// <param name="y">Top edge of this box's border box. Margins are the caller's business.</param>
    /// <param name="containingWidth">The containing block's content width.</param>
    /// <param name="fonts">The faces available for measuring text.</param>
    /// <param name="containingHeight">
    /// The containing block's CONTENT height, when that is definite, and null when it is not.
    /// What a percentage <c>height</c>, <c>min-height</c> or <c>max-height</c> resolves against —
    /// CSS 2.1 §10.5 makes such a percentage behave as <c>auto</c> when there is nothing definite
    /// to resolve it against, which is why this is nullable rather than defaulted.
    /// </param>
    /// <param name="assignedHeight">
    /// A content height to use in place of the one the content came to, for a caller that has
    /// already decided how tall the box is. Only <see cref="AbsoluteLayout"/> passes it, for a box
    /// stretched between <c>top</c> and <c>bottom</c> — the vertical mirror of
    /// <paramref name="assignedWidth"/>, and the reason an auto height can now be settled by
    /// something other than the content. A DECLARED height still wins, since a caller only passes
    /// this where there is none.
    /// </param>
    /// <param name="assignedWidth">
    /// A width to use instead of resolving one, for a box whose width its container decided. A
    /// table cell has one, its column having settled the width before the cell was reached; so
    /// does a float, whose shrink-to-fit width is computed before it is positioned. Resolving
    /// either here from <c>width: auto</c> would fill the containing block instead.
    /// </param>
    /// <param name="floats">
    /// The floats in the enclosing block formatting context, which lines are placed around and new
    /// floats are placed clear of. Null starts a fresh context, which is what a box establishing
    /// its own formatting context wants.
    /// </param>
    public static float Layout(
        LayoutBox box,
        float x,
        float y,
        float containingWidth,
        FontSet fonts,
        float? assignedWidth = null,
        FloatContext? floats = null,
        float? assignedHeight = null,
        float? containingHeight = null)
    {
        var style = box.Style;

        // A box that establishes its own formatting context sees no float from outside it and
        // leaks none outward. Everything else shares its parent's, which is what lets a float
        // declared in one block shorten the lines of a later sibling.
        var ownsFloats = floats is null;
        floats ??= new();

        // A table sizes its columns before it can size anything in them, so it takes over from
        // here rather than being a block with unusual children.
        if (style.Display == DisplayKind.Table)
        {
            return TableLayout.Layout(box, x, y, containingWidth, fonts);
        }

        var paddingLeft = style.PaddingLeft.Resolve(containingWidth);
        var paddingRight = style.PaddingRight.Resolve(containingWidth);
        var paddingTop = style.PaddingTop.Resolve(containingWidth);
        var paddingBottom = style.PaddingBottom.Resolve(containingWidth);

        var surround = paddingLeft + paddingRight + style.BorderWidthX;

        // A replaced box sizes from its own content rather than from its container: an auto width
        // takes the image's intrinsic width instead of filling the line, and the two dimensions
        // are tied by the aspect ratio. Margins are still resolved the ordinary way afterwards, so
        // `margin: 0 auto` centres an image exactly as it centres a div.
        float? replacedWidth = null;
        float? replacedHeight = null;

        if (box.Image is {} replaced)
        {
            var size = ReplacedSizing.Resolve(
                style,
                replaced,
                containingWidth,
                surround,
                paddingTop + paddingBottom + style.BorderWidthY);
            replacedWidth = size.Width;
            replacedHeight = size.Height;
        }

        var (marginLeft, contentWidth) = (assignedWidth, replacedWidth) switch
        {
            ({} given, _) => (0f, Math.Max(0, given - surround)),
            (_, {} fixedWidth) => (ResolveReplacedMargin(style, containingWidth, surround, fixedWidth), fixedWidth),
            _ => ResolveHorizontal(style, containingWidth, surround)
        };

        var borderBoxWidth = contentWidth + paddingLeft + paddingRight + style.BorderWidthX;
        var borderBoxX = x + marginLeft;
        var contentX = borderBoxX + style.BorderLeft + paddingLeft;
        var contentY = y + style.BorderTop + paddingTop;

        var surroundY = paddingTop + paddingBottom + style.BorderWidthY;

        // Settled BEFORE the subtree, which is the whole reason a percentage height works at all:
        // a child resolves its own percentage against this box's content height, and that answer
        // has to exist before the child is laid out. It can, because a definite height is one this
        // box was TOLD — declared, resolved against its own containing block, or handed down by an
        // absolute box's offsets — rather than one its content came to.
        var declared = replacedHeight ?? Definite(style.Height, containingHeight, style, surroundY) ?? assignedHeight;

        // And what the children see. Clamped, because the used height is what they resolve
        // against; null when this box has no definite height of its own, which stops the
        // percentage in its tracks exactly as CSS asks.
        var inner = declared is {} settled
            ? ClampHeight(settled, style, surroundY, containingHeight)
            : (float?) null;

        float contentHeight;

        if (replacedHeight is {} imageHeight)
        {
            contentHeight = imageHeight;
        }
        else if (box.IsInlineContainer)
        {
            // Every float this box declares is placed before its lines are flowed, because each
            // line asks the context how much room is left beside them. They go at the content top:
            // a float written between two words belongs on the line carrying those words, and this
            // box has not flowed any lines yet to know where that is.
            PlaceFloats(box, 0, box.Floats.Count, contentX, contentY, contentWidth, fonts, floats, inner);
            NoteStatic(box, 0, box.Positioned.Count, contentX, contentY);

            contentHeight = InlineLayout.Layout(box, contentX, contentY, contentWidth, fonts, floats, inner);

            // Inline layout works from a zero origin so it never has to know where the block
            // ended up; the lines are moved into place once, here.
            foreach (var line in box.Lines)
            {
                line.Translate(contentX, contentY);
            }
        }
        else
        {
            contentHeight = LayoutChildren(box, contentX, contentY, contentWidth, fonts, floats, inner);
        }

        // A box that established this formatting context grows to contain the floats in it; every
        // other box lets them overflow. That asymmetry is CSS 2.1 §10.6.7 and it is measurable:
        // in `float/basic` the root reaches 304px to enclose a float that hangs out of a wrapper
        // ending at 260px, and the wrapper stays 260px. Getting it backwards either clips a
        // trailing float off the last page or pads every block that holds one.
        if (ownsFloats)
        {
            contentHeight = Math.Max(contentHeight, floats.Bottom(contentY) - contentY);
        }

        // A declared height wins over the aspect ratio, which wins over what the content came to.
        // A replaced box's height already came from the ratio, so it wins over all three.
        var height = ClampHeight(
            declared ?? Ratio(style, borderBoxWidth, surroundY) ?? contentHeight,
            style,
            surroundY,
            containingHeight);

        var borderBoxHeight = height + paddingTop + paddingBottom + style.BorderWidthY;

        box.BorderBox = new(borderBoxX, y, borderBoxWidth, borderBoxHeight);
        box.ContentBox = new(contentX, contentY, contentWidth, height);

        // After the box's own geometry, and after its subtree's: a marker hangs off the item's
        // border edge and sits on the item's FIRST LINE, and that line may be several blocks down
        // and below a margin that collapsed through. Neither is known any earlier than here.
        ListMarkers.Place(box, fonts);

        // Relative positioning is applied last and to the whole subtree, because it is a paint-time
        // shift rather than a layout one: the box keeps the space it was given, the height returned
        // is the height before the offset, and nothing measured against it moves. That is what
        // makes it cheap — it invalidates no measurement, so no ancestor has to be told.
        //
        // Floats inside the subtree move with it while the entries recorded for them in the float
        // context do not, which is also right: content outside flows as though the offset had never
        // happened.
        Relocate(box, containingWidth);

        return borderBoxHeight;
    }

    /// <summary>
    /// Lays out <paramref name="parent"/>'s block children, collapsing margins between them, and
    /// returns the content height.
    /// </summary>
    static float LayoutChildren(
        LayoutBox parent,
        float contentX,
        float contentY,
        float contentWidth,
        FontSet fonts,
        FloatContext floats,
        float? contentHeight)
    {
        var y = 0f;
        var pending = CollapsedMargin.Empty;

        // Whether the margin collapsing out through the parent's top edge is still being skipped.
        // It covers a RUN of self-collapsing children rather than only the first, because
        // `LeadingMargin` walks that whole run when the ancestor applies the margin — so every
        // margin in the run has already been applied by the time this gets here.
        var escaping = IsTopOpen(parent, contentWidth);

        var placed = 0;
        var noted = 0;

        for (var index = 0; index < parent.Children.Count; index++)
        {
            var child = parent.Children[index];

            // Floats declared ahead of this child go down first, at the flow position reached so
            // far. A float written after two paragraphs starts below them, and one written before
            // any of them starts at the top.
            placed = PlaceFloats(parent, placed, index, contentX, contentY + y, contentWidth, fonts, floats, contentHeight);

            // The same position, recorded rather than used. An absolutely positioned box takes no
            // space, so this is the only moment where "the place flow would have given it" exists —
            // and that is where it goes when its offsets are auto.
            noted = NoteStatic(parent, noted, index, contentX, contentY + y);

            pending = pending.Merge(LeadingMargin(child, contentWidth));

            if (escaping)
            {
                // This margin collapsed out through the parent's top edge, so it was already
                // applied by whoever positioned the parent. Applying it again would double it.
                pending = CollapsedMargin.Empty;
            }

            // Where the flow stood before the margin was applied, which is what a self-collapsing
            // child has to be able to return to.
            var open = y;

            y += pending.Value;

            // Clearance, applied after the collapsed margin has been added rather than instead of
            // it: `clear` moves the box down to the bottom of the floats it names, and a margin
            // that already carried it further stands.
            var cleared = floats.ClearTo(child.Style.Clear, contentY + y);
            var clearance = cleared - contentY - y;
            y = cleared - contentY;

            y += Place(child, contentX, contentY + y, contentWidth, fonts, floats, contentHeight);

            // A self-collapsing box does not SEPARATE the margin above it from the margin below:
            // the two join one collapsed set, which is applied once — to whatever comes after it
            // rather than to the box itself. So the flow position goes back to where the margin
            // started and the box keeps the position the partial collapse gave it, which is what a
            // browser reports for such a box.
            //
            // Applying it twice is what an empty `<div>` between two paragraphs used to do: with a
            // 40px margin above and a 50px margin of its own, everything after it sat 90px down
            // where 50 belongs. CSS 2.1 §8.3.1's collapse-through, and nothing in the corpus held
            // an empty box between two boxes with margins until `float/clearance` did.
            //
            // CLEARANCE takes it out of that rule, which is §8.3.1's own wording: two margins are
            // adjoining only when no clearance separates them, so a box that took any keeps its
            // margins apart like a box with a border.
            if (clearance == 0 && IsSelfCollapsing(child, contentWidth))
            {
                y = open;

                if (!escaping)
                {
                    pending = pending.Merge(TrailingMargin(child, contentWidth));
                }

                continue;
            }

            pending = TrailingMargin(child, contentWidth);
            escaping = false;
        }

        // Out-of-flow boxes declared after the last in-flow child, which is where a trailing float
        // lands and where a trailing absolute box would have gone.
        PlaceFloats(parent, placed, parent.Floats.Count, contentX, contentY + y, contentWidth, fonts, floats, contentHeight);
        NoteStatic(parent, noted, parent.Positioned.Count, contentX, contentY + y);

        // A trailing margin escapes downward only when nothing stops it: no bottom border, no
        // bottom padding, and an auto height. Otherwise it is trapped inside and counts toward
        // the content height.
        if (!IsBottomOpen(parent, contentWidth))
        {
            y += pending.Value;
        }

        return y;
    }

    /// <summary>
    /// Lays out one in-flow block child, against the floats beside it when it establishes a
    /// formatting context of its own.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An ordinary block keeps its full width and simply overlaps a float, with only the lines
    /// inside it shortened — the single most surprising rule about floats, and the one
    /// <c>float/basic</c> exists to pin. A box that establishes a block formatting context is the
    /// exception: its border box is narrowed and shifted so it sits BESIDE the float instead.
    /// </para>
    /// <para>
    /// The band is sampled at the child's top edge alone, and the box keeps that width for its
    /// whole height even where the float ends above it. Measured: in <c>float/overflow_bfc</c> the
    /// container is 60px tall beside a 90px float and stays 696px wide, so nothing re-widens it
    /// below the float's bottom.
    /// </para>
    /// <para>
    /// Only an AUTO width is narrowed. A declared width is honoured as declared and the box is
    /// left where it is — a browser shifts it sideways as well, which is a further rule this does
    /// not implement and which no scenario measures. Percentages still resolve against the
    /// containing block rather than against the band, which is why <c>containingWidth</c> is
    /// passed through unchanged and the band reaches
    /// <see cref="Layout"/> as an assigned width instead.
    /// </para>
    /// </remarks>
    static float Place(
        LayoutBox child,
        float contentX,
        float top,
        float contentWidth,
        FontSet fonts,
        FloatContext floats,
        float? contentHeight)
    {
        if (!child.Style.EstablishesContext)
        {
            return Layout(
                child,
                contentX,
                top,
                contentWidth,
                fonts,
                floats: floats,
                containingHeight: contentHeight);
        }

        // An infinitesimally thin slice at the child's top edge, rather than a zero-height one:
        // `FloatContext.Band` treats its range as half-open, so a zero-height query overlaps
        // nothing at all and every box would come back full width.
        var (left, right) = floats.Band(
            top,
            MathF.BitIncrement(top),
            contentX,
            contentX + contentWidth);
        var available = Math.Max(0, right - left);

        // No float beside it, so nothing to avoid and the ordinary path applies — which also keeps
        // `margin: auto` centring a box that happens to carry `overflow: hidden`.
        if (available >= contentWidth || child.Style.Width.Kind != LengthKind.Auto)
        {
            return Layout(
                child,
                contentX,
                top,
                contentWidth,
                fonts,
                floats: null,
                containingHeight: contentHeight);
        }

        var margins =
            child.Style.MarginLeft.Resolve(contentWidth) +
            child.Style.MarginRight.Resolve(contentWidth);

        return Layout(
            child,
            left,
            top,
            contentWidth,
            fonts,
            assignedWidth: Math.Max(0, available - margins),
            floats: null);
    }

    /// <summary>
    /// Applies a relatively positioned box's offsets to it and its subtree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CSS 2.1 §9.4.3: <c>top</c> wins over <c>bottom</c> and <c>left</c> over <c>right</c> when
    /// both are given, and a lone <c>bottom</c> or <c>right</c> moves the box the other way — the
    /// offsets name the direction the box moves AWAY from that edge, so `bottom: 5px` lifts it.
    /// </para>
    /// <para>
    /// Percentages resolve against the containing block's WIDTH on all four sides, vertical ones
    /// included. That reads like a mistake and is what the specification says.
    /// </para>
    /// </remarks>
    static void Relocate(LayoutBox box, float containingWidth)
    {
        if (box.Style.Position != PositionKind.Relative)
        {
            return;
        }

        var style = box.Style;

        var dx = style.Left.ResolveOrNull(containingWidth) ??
                 -style.Right.ResolveOrNull(containingWidth) ??
                 0;

        var dy = style.Top.ResolveOrNull(containingWidth) ??
                 -style.Bottom.ResolveOrNull(containingWidth) ??
                 0;

        if (dx != 0 || dy != 0)
        {
            box.Translate(dx, dy);
        }
    }

    /// <summary>
    /// Records the static position of the absolutely positioned boxes declared between two in-flow
    /// positions, and returns how many have now been recorded.
    /// </summary>
    static int NoteStatic(LayoutBox parent, int from, int until, float x, float y)
    {
        while (from < parent.Positioned.Count && parent.Positioned[from].Index <= until)
        {
            parent.Positioned[from].Box.StaticPosition = (x, y);
            from++;
        }

        return from;
    }

    /// <summary>
    /// Places the floats declared between two in-flow positions, and returns how many have now
    /// been placed.
    /// </summary>
    /// <remarks>
    /// Called as the flow advances rather than once up front, because a float starts at the
    /// position it was declared at. <paramref name="top"/> is that position in absolute
    /// coordinates.
    /// </remarks>
    static int PlaceFloats(
        LayoutBox parent,
        int from,
        int until,
        float contentX,
        float top,
        float contentWidth,
        FontSet fonts,
        FloatContext floats,
        float? contentHeight)
    {
        while (from < parent.Floats.Count && parent.Floats[from].Index <= until)
        {
            PlaceFloat(parent.Floats[from].Box, contentX, top, contentWidth, fonts, floats, contentHeight);
            from++;
        }

        return from;
    }

    /// <summary>
    /// Sizes one float, finds it a position, and moves it there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Laid out first and positioned second, which is the only order available: the height decides
    /// which vertical positions it fits in, and the height is not known until its contents have
    /// been flowed. So it is laid out at a provisional origin, measured, placed, and translated by
    /// the difference.
    /// </para>
    /// <para>
    /// A float establishes its own formatting context, so its contents are flowed against a fresh
    /// <see cref="FloatContext"/> — the floats outside it are not in its way, and its own do not
    /// reach out.
    /// </para>
    /// <para>
    /// Auto margins on a float compute to zero rather than centring it (CSS 2.1 §9.5), which is
    /// why they are resolved directly here instead of through
    /// <see cref="ResolveHorizontal"/>.
    /// </para>
    /// </remarks>
    static void PlaceFloat(
        LayoutBox box,
        float contentX,
        float top,
        float contentWidth,
        FontSet fonts,
        FloatContext floats,
        float? contentHeight)
    {
        var style = box.Style;
        var marginTop = style.MarginTop.Resolve(contentWidth);
        var marginBottom = style.MarginBottom.Resolve(contentWidth);
        var marginLeft = style.MarginLeft.Resolve(contentWidth);
        var marginRight = style.MarginRight.Resolve(contentWidth);

        var height = Layout(
            box,
            0,
            0,
            contentWidth,
            fonts,
            ShrinkToFit(box, contentWidth, fonts),
            containingHeight: contentHeight);
        var width = box.BorderBox.Width;

        // Clearance first, then the sideways search: a float carrying `clear` starts below what it
        // clears and is only then pushed as far to its side as it will go.
        //
        // The flow position is where the MARGIN box goes, not the border box. Adding the top
        // margin here as well as when translating below would apply it twice, and a float with
        // `margin-top: 10px` would sit 20px down.
        var start = floats.ClearTo(style.Clear, top);

        var margin = floats.Place(
            style.Float,
            start,
            width + marginLeft + marginRight,
            height + marginTop + marginBottom,
            contentX,
            contentX + contentWidth);

        box.Translate(
            margin.X + marginLeft - box.BorderBox.X,
            margin.Y + marginTop - box.BorderBox.Y);
    }

    /// <summary>
    /// The border-box width a float takes, or null to let the ordinary rules decide.
    /// </summary>
    /// <remarks>
    /// CSS 2.1 §10.3.5 gives a float with an auto width its shrink-to-fit width:
    /// <c>min(max(min-content, available), max-content)</c>. In words, it takes what it wants
    /// unless that will not fit, in which case it takes what is left — but never squeezes below
    /// its longest unbreakable word, which is why a narrow container leaves a float overflowing
    /// rather than mangled.
    /// </remarks>
    public static float? ShrinkToFit(LayoutBox box, float contentWidth, FontSet fonts)
    {
        // A replaced float sizes from its image, and a declared width needs no help. Both are
        // handled by the ordinary path, which also applies the aspect ratio.
        if (box.Image is not null || box.Style.Width.Kind != LengthKind.Auto)
        {
            return null;
        }

        var available = Math.Max(
            0,
            contentWidth -
            box.Style.MarginLeft.Resolve(contentWidth) -
            box.Style.MarginRight.Resolve(contentWidth));

        var (min, max) = IntrinsicWidths.Measure(box, fonts);
        return Math.Min(Math.Max(min, available), max);
    }

    /// <summary>
    /// Resolves the horizontal box model, returning the used left margin and content width.
    /// </summary>
    /// <remarks>
    /// CSS 2.1 §10.3.3: the margins, borders, padding and width of a block-level box in normal
    /// flow must sum to the containing block's width. The equation is over-determined, so
    /// something has to give, and which one depends on what was left <c>auto</c> — an auto width
    /// absorbs everything, two auto margins centre the box, and if nothing is auto the right
    /// margin is overridden to make the sum work out.
    /// </remarks>
    static (float MarginLeft, float ContentWidth) ResolveHorizontal(
        ComputedStyle style,
        float containingWidth,
        float surround)
    {
        var available = Math.Max(0, containingWidth - surround);
        var marginLeft = style.MarginLeft.ResolveOrNull(containingWidth);
        var marginRight = style.MarginRight.ResolveOrNull(containingWidth);

        // Under `border-box` the declared width is the border box, so the padding and border come
        // out of it here and everything below this line is a content width as before.
        var width = style.ContentSize(style.Width.ResolveOrNull(containingWidth), surround);

        var tentative = width ?? Math.Max(0, available - (marginLeft ?? 0) - (marginRight ?? 0));
        var used = Clamp(tentative, style, containingWidth, surround);

        // An auto width that survived clamping absorbs the remaining space, and auto margins
        // become zero rather than competing with it. But if min/max-width overrode it, the
        // specification says to re-run the algorithm treating the clamped value as a specified
        // width — which hands the leftover space back to the margins. That is the whole mechanism
        // behind `max-width` with `margin: 0 auto`, the most common centring idiom there is.
        if (width is null && used == tentative)
        {
            return (marginLeft ?? 0, used);
        }

        var slack = available - used;

        return (marginLeft, marginRight) switch
        {
            (null, null) => (Math.Max(0, slack / 2), used),
            (null, not null) => (Math.Max(0, slack - marginRight.Value), used),
            // Both specified is the over-constrained case, and it shares its outcome with the
            // auto-right case: the left margin is honoured and the right absorbs the difference.
            // For left-to-right text that is what CSS 2.1 §10.3.3 requires.
            _ => (marginLeft.Value, used)
        };
    }

    /// <summary>
    /// The left margin for a block-level replaced box, whose width is already settled.
    /// </summary>
    /// <remarks>
    /// The same auto-margin rules as an ordinary block — two autos centre, one absorbs — applied
    /// to a width that came from the image rather than from the container. That is what makes
    /// <c>img { display: block; margin: 0 auto }</c> centre a picture.
    /// </remarks>
    static float ResolveReplacedMargin(
        ComputedStyle style,
        float containingWidth,
        float surround,
        float width)
    {
        var slack = Math.Max(0, containingWidth - surround - width);
        var marginLeft = style.MarginLeft.ResolveOrNull(containingWidth);
        var marginRight = style.MarginRight.ResolveOrNull(containingWidth);

        return (marginLeft, marginRight) switch
        {
            (null, null) => slack / 2,
            (null, not null) => Math.Max(0, slack - marginRight.Value),
            _ => marginLeft.Value
        };
    }

    /// <summary>Applies <c>min-width</c> and <c>max-width</c>, in that precedence.</summary>
    /// <remarks>
    /// Both measure whatever <c>box-sizing</c> says, as <c>width</c> does, so they are deflated to
    /// content widths before being compared against one. A <c>max-width</c> under
    /// <c>border-box</c> that is narrower than the box's own padding clamps the content to zero
    /// rather than to a negative width.
    /// </remarks>
    static float Clamp(float width, ComputedStyle style, float containingWidth, float surround)
    {
        if (style.ContentSize(style.MaxWidth.ResolveOrNull(containingWidth), surround) is {} max)
        {
            width = Math.Min(width, max);
        }

        // After max-width, so a min wider than the max wins — which is the order CSS specifies.
        if (style.ContentSize(style.MinWidth.ResolveOrNull(containingWidth), surround) is {} min)
        {
            width = Math.Max(width, min);
        }

        return width;
    }

    /// <summary>
    /// The content height an <c>aspect-ratio</c> asks for, or null when there is none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ratio applies to the box named by <c>box-sizing</c>, which for the default
    /// <c>content-box</c> means the content box and for <c>border-box</c> the border box — so it
    /// goes through the same deflation a declared height does rather than beside it. Measured with
    /// a 200px box at <c>4 / 1</c>, which is 50px tall.
    /// </para>
    /// <para>
    /// Only when the height is AUTO. A declared height wins outright, the ratio supplying what was
    /// not given — which is what makes <c>aspect-ratio</c> safe to put on a rule that some elements
    /// also size explicitly.
    /// </para>
    /// </remarks>
    static float? Ratio(ComputedStyle style, float borderBoxWidth, float surroundY)
    {
        if (style.AspectRatio <= 0)
        {
            return null;
        }

        return style.ContentSize(borderBoxWidth / style.AspectRatio, surroundY);
    }

    /// <summary>Applies <c>min-height</c> and <c>max-height</c>, in that precedence.</summary>
    /// <remarks>
    /// A percentage resolves against <paramref name="containing"/> and is SKIPPED when there is
    /// none, which is CSS 2.1 §10.7's rule: a percentage against an indefinite containing height
    /// behaves as though the property were not there.
    ///
    /// Shortening a box here does not by itself hide what it holds: content keeps drawing past the
    /// bottom edge, which is what <c>overflow: visible</c> asks for. A box that clips is the one
    /// declaring <c>overflow</c>, and it clips in the painter rather than here.
    /// </remarks>
    static float ClampHeight(float height, ComputedStyle style, float surround, float? containing)
    {
        if (Definite(style.MaxHeight, containing, style, surround) is {} max)
        {
            height = Math.Min(height, max);
        }

        // After max-height, so a minimum taller than the maximum wins — the order CSS specifies,
        // and the one `Clamp` uses for the horizontal pair.
        if (Definite(style.MinHeight, containing, style, surround) is {} min)
        {
            height = Math.Max(height, min);
        }

        return height;
    }

    /// <summary>
    /// One of the three height properties as a CONTENT height, or null when it settles nothing.
    /// </summary>
    /// <remarks>
    /// The one place a vertical percentage is resolved. It goes through <c>box-sizing</c> like
    /// every other declared size, because the property means the same thing however it was written
    /// — <c>height: 50%</c> under <c>border-box</c> names half the containing block's height as a
    /// BORDER box, and the surround comes out of it the same way an absolute length's would.
    /// </remarks>
    static float? Definite(CssLength length, float? containing, ComputedStyle style, float surround) =>
        length.Kind switch
        {
            LengthKind.Absolute => style.ContentSize(length.Value, surround),
            LengthKind.Percent or LengthKind.Calc when containing is {} basis =>
                style.ContentSize(Math.Max(0, length.Resolve(basis)), surround),
            _ => null
        };

    /// <summary>
    /// The margin that collapses out through <paramref name="box"/>'s top edge.
    /// </summary>
    /// <remarks>
    /// Its own top margin, plus every descendant top margin that reaches the edge unobstructed. A
    /// border or padding on the top edge stops the collapse, and so does inline content, because
    /// both put something between the margin and the edge.
    /// </remarks>
    public static CollapsedMargin LeadingMargin(LayoutBox box, float containingWidth)
    {
        var margin = CollapsedMargin.Of(box.Style.MarginTop.Resolve(containingWidth));

        if (!IsTopOpen(box, containingWidth) || box.IsInlineContainer)
        {
            return margin;
        }

        foreach (var child in box.Children)
        {
            margin = margin.Merge(LeadingMargin(child, containingWidth));

            if (!IsSelfCollapsing(child, containingWidth))
            {
                break;
            }

            // A box with no height and no boundaries does not separate its own margins, so its
            // bottom margin joins the same collapsed set and the walk continues past it.
            margin = margin.Merge(TrailingMargin(child, containingWidth));
        }

        return margin;
    }

    /// <summary>
    /// The margin that collapses out through <paramref name="box"/>'s bottom edge.
    /// </summary>
    public static CollapsedMargin TrailingMargin(LayoutBox box, float containingWidth)
    {
        var margin = CollapsedMargin.Of(box.Style.MarginBottom.Resolve(containingWidth));

        if (!IsBottomOpen(box, containingWidth) || box.IsInlineContainer)
        {
            return margin;
        }

        for (var index = box.Children.Count - 1; index >= 0; index--)
        {
            var child = box.Children[index];
            margin = margin.Merge(TrailingMargin(child, containingWidth));

            if (!IsSelfCollapsing(child, containingWidth))
            {
                break;
            }

            margin = margin.Merge(LeadingMargin(child, containingWidth));
        }

        return margin;
    }

    /// <summary>
    /// Whether a margin can collapse through the box's top edge.
    /// </summary>
    /// <remarks>
    /// Never for the root. CSS 2.1 §8.3.1 exempts the root element's margins from collapsing, and
    /// the rule is not a technicality here: a margin that escapes the root has no ancestor left to
    /// apply it, so it would simply be lost and the document would start flush against the page.
    /// </remarks>
    static bool IsTopOpen(LayoutBox box, float containingWidth) =>
        !box.IsRoot &&
        box.Style.Display != DisplayKind.Table &&
        box.Style.BorderTop == 0 &&
        box.Style.PaddingTop.Resolve(containingWidth) == 0;

    /// <summary>
    /// Whether a margin can collapse through the box's bottom edge.
    /// </summary>
    /// <remarks>
    /// A definite height closes the edge as surely as a border does: the box's own bottom is fixed
    /// by the height, so a child's bottom margin has nowhere to escape to.
    /// </remarks>
    static bool IsBottomOpen(LayoutBox box, float containingWidth) =>
        !box.IsRoot &&
        box.Style.Display != DisplayKind.Table &&
        box.Style.BorderBottom == 0 &&
        box.Style.PaddingBottom.Resolve(containingWidth) == 0 &&
        box.Style.Height.Kind != LengthKind.Absolute;

    /// <summary>
    /// Whether the box's own top and bottom margins adjoin each other — a box with nothing in it
    /// to hold them apart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A replaced box never is, however its height was arrived at. Its content holds the margins
    /// apart exactly as a declared height would, and the height test below cannot see that: an
    /// image sized from its aspect ratio has <c>height: auto</c>, which reads as zero here. Without
    /// this an image's own bottom margin collapses through it and pushes the image down by that
    /// margin.
    /// </para>
    /// <para>
    /// Nor is a box sized by <c>aspect-ratio</c>, which is the same trap reached by a second route
    /// and cost the same debugging round. Its <c>height</c> is auto and its content is empty, so
    /// every test below passes and the box reads as having nothing in it — while it is in fact
    /// fifty pixels tall. `block/aspect_ratio` had its whole page six pixels low from the first
    /// box's own bottom margin collapsing through it and becoming a leading margin for the run.
    /// </para>
    /// </remarks>
    static bool IsSelfCollapsing(LayoutBox box, float containingWidth) =>
        box.Image is null &&
        box.Style.AspectRatio <= 0 &&
        box.Style.Display != DisplayKind.Table &&
        box.Style.BorderWidthY == 0 &&
        box.Style.PaddingTop.Resolve(containingWidth) == 0 &&
        box.Style.PaddingBottom.Resolve(containingWidth) == 0 &&
        box.Style.Height.Resolve(0) == 0 &&
        box.Style.MinHeight.Resolve(0) == 0 &&
        !box.IsInlineContainer &&
        box.Children.All(_ => IsSelfCollapsing(_, containingWidth));
}
