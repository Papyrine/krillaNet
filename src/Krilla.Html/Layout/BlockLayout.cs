namespace Krilla.Html.Layout;

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
/// Floats and positioned boxes are not implemented. Everything is in normal flow, so nothing
/// shortens a line box and nothing escapes its parent.
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
    public static float Layout(LayoutBox box, float x, float y, float containingWidth, FontSet fonts)
    {
        var style = box.Style;

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
            var size = ReplacedSizing.Resolve(style, replaced, containingWidth - surround);
            replacedWidth = size.Width;
            replacedHeight = size.Height;
        }

        var (marginLeft, contentWidth) = replacedWidth is {} fixedWidth
            ? (ResolveReplacedMargin(style, containingWidth, surround, fixedWidth), fixedWidth)
            : ResolveHorizontal(style, containingWidth, surround);

        var borderBoxWidth = contentWidth + paddingLeft + paddingRight + style.BorderWidthX;
        var borderBoxX = x + marginLeft;
        var contentX = borderBoxX + style.BorderLeft + paddingLeft;
        var contentY = y + style.BorderTop + paddingTop;

        float contentHeight;

        if (replacedHeight is {} imageHeight)
        {
            contentHeight = imageHeight;
        }
        else if (box.IsInlineContainer)
        {
            contentHeight = InlineLayout.Layout(box, contentWidth, fonts);

            // Inline layout works from a zero origin so it never has to know where the block
            // ended up; the lines are moved into place once, here.
            foreach (var line in box.Lines)
            {
                line.Translate(contentX, contentY);
            }
        }
        else
        {
            contentHeight = LayoutChildren(box, contentX, contentY, contentWidth, fonts);
        }

        // A percentage height resolves against the containing block's height, which is not known
        // here and in the common case is itself auto. Treating it as auto matches what CSS
        // requires whenever the containing height is indefinite, which is the case throughout a
        // paginated document.
        //
        // A replaced box's height already came from the aspect ratio, so it wins over both.
        var height = replacedHeight ??
                     (style.Height.Kind == LengthKind.Absolute ? style.Height.Value : contentHeight);

        var borderBoxHeight = height + paddingTop + paddingBottom + style.BorderWidthY;

        box.BorderBox = new(borderBoxX, y, borderBoxWidth, borderBoxHeight);
        box.ContentBox = new(contentX, contentY, contentWidth, height);

        // After the box's own geometry, and after its subtree's: a marker hangs off the item's
        // border edge and sits on the item's FIRST LINE, and that line may be several blocks down
        // and below a margin that collapsed through. Neither is known any earlier than here.
        ListMarkers.Place(box, fonts);

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
        FontSet fonts)
    {
        var y = 0f;
        var pending = CollapsedMargin.Empty;
        var openTop = IsTopOpen(parent, contentWidth);
        var first = true;

        foreach (var child in parent.Children)
        {
            pending = pending.Merge(LeadingMargin(child, contentWidth));

            if (first && openTop)
            {
                // This margin collapsed out through the parent's top edge, so it was already
                // applied by whoever positioned the parent. Applying it again would double it.
                pending = CollapsedMargin.Empty;
            }

            y += pending.Value;
            y += Layout(child, contentX, contentY + y, contentWidth, fonts);
            pending = TrailingMargin(child, contentWidth);
            first = false;
        }

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
        var width = style.Width.ResolveOrNull(containingWidth);

        var tentative = width ?? Math.Max(0, available - (marginLeft ?? 0) - (marginRight ?? 0));
        var used = Clamp(tentative, style, containingWidth);

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
            _ => (marginLeft!.Value, used)
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
            _ => marginLeft!.Value
        };
    }

    /// <summary>Applies <c>min-width</c> and <c>max-width</c>, in that precedence.</summary>
    static float Clamp(float width, ComputedStyle style, float containingWidth)
    {
        if (style.MaxWidth.ResolveOrNull(containingWidth) is {} max)
        {
            width = Math.Min(width, max);
        }

        // After max-width, so a min wider than the max wins — which is the order CSS specifies.
        if (style.MinWidth.ResolveOrNull(containingWidth) is {} min)
        {
            width = Math.Max(width, min);
        }

        return width;
    }

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
        box.Style.BorderBottom == 0 &&
        box.Style.PaddingBottom.Resolve(containingWidth) == 0 &&
        box.Style.Height.Kind != LengthKind.Absolute;

    /// <summary>
    /// Whether the box's own top and bottom margins adjoin each other — a box with nothing in it
    /// to hold them apart.
    /// </summary>
    /// <remarks>
    /// A replaced box never is, however its height was arrived at. Its content holds the margins
    /// apart exactly as a declared height would, and the height test below cannot see that: an
    /// image sized from its aspect ratio has <c>height: auto</c>, which reads as zero here. Without
    /// this an image's own bottom margin collapses through it and pushes the image down by that
    /// margin.
    /// </remarks>
    static bool IsSelfCollapsing(LayoutBox box, float containingWidth) =>
        box.Image is null &&
        box.Style.BorderWidthY == 0 &&
        box.Style.PaddingTop.Resolve(containingWidth) == 0 &&
        box.Style.PaddingBottom.Resolve(containingWidth) == 0 &&
        box.Style.Height.Resolve(0) == 0 &&
        !box.IsInlineContainer &&
        box.Children.All(_ => IsSelfCollapsing(_, containingWidth));
}
