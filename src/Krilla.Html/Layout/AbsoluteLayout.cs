namespace Krilla.Html.Layout;

/// <summary>
/// Positions absolutely positioned boxes, once normal flow has finished.
/// </summary>
/// <remarks>
/// <para>
/// A separate pass rather than part of <see cref="BlockLayout"/>, because the two need the tree in
/// opposite orders. An absolute box is positioned against its nearest positioned ancestor, so it
/// cannot be placed until that ancestor has been sized — and the ancestor is sized by flowing
/// children that may themselves declare absolute boxes. Running flow to completion first and
/// descending afterwards breaks that circle without any deferral machinery.
/// </para>
/// <para>
/// The containing block is the ancestor's PADDING box, not its content box and not its border box.
/// Measured, not assumed: an absolute box with <c>top: 0; left: 0</c> inside a relatively
/// positioned parent with a 5px border and 10px padding lands on the inside of the border and the
/// outside of the padding.
/// </para>
/// <para>
/// A static ancestor between the box and its containing block is skipped entirely — its own
/// position, margins and padding contribute nothing. That is what makes
/// <c>position: relative</c> on an outer element the standard way to anchor something several
/// levels down.
/// </para>
/// <para>
/// A FIXED box skips every ancestor, positioned or not: its containing block is the initial one,
/// which in paged media is the page. That is the single thing distinguishing it from an absolute
/// box, so treating the two alike is wrong by exactly the offset of the nearest positioned
/// ancestor — invisible whenever there is none, which is most documents.
/// <c>position/fixed</c> measures it.
/// </para>
/// </remarks>
static class AbsoluteLayout
{
    /// <summary>
    /// Places every absolutely positioned box under <paramref name="root"/>.
    /// </summary>
    /// <param name="root">The root box, already laid out.</param>
    /// <param name="initial">
    /// The initial containing block: what an absolute box with no positioned ancestor is placed
    /// against. In paged media that is the page's content area rather than a scrollable viewport.
    /// </param>
    /// <param name="fonts">The faces available for measuring text.</param>
    public static void Place(LayoutBox root, Rect initial, FontSet fonts) =>
        Descend(root, initial, initial, fonts);

    static void Descend(LayoutBox box, Rect containing, Rect initial, FontSet fonts)
    {
        // A positioned box becomes the containing block for everything below it; a static one
        // passes its own containing block straight through.
        var inner = box.Style.IsPositioned ? PaddingBox(box) : containing;

        foreach (var child in box.Children)
        {
            Descend(child, inner, initial, fonts);
        }

        foreach (var floated in box.Floats)
        {
            Descend(floated.Box, inner, initial, fonts);
        }

        // An inline-block is reached through the line that holds it rather than through Children,
        // and an absolute box declared inside one still has to be placed.
        foreach (var line in box.Lines)
        {
            foreach (var atomic in line.Boxes)
            {
                Descend(atomic, inner, initial, fonts);
            }
        }

        foreach (var positioned in box.Positioned)
        {
            // A fixed box is anchored to the page rather than to whatever it happens to sit
            // inside, so the ancestor chain this walk has been accumulating does not apply to it.
            var against = positioned.Box.Style.Position == PositionKind.Fixed ? initial : inner;

            // Placed before descending into it, because its own descendants are positioned against
            // the box it has just been given.
            Position(positioned.Box, against, fonts);
            Descend(positioned.Box, against, initial, fonts);
        }
    }

    /// <summary>
    /// Sizes and positions one absolute box against <paramref name="containing"/>.
    /// </summary>
    /// <remarks>
    /// Laid out at a provisional origin and moved afterwards, the same order floats use and for the
    /// same reason: a <c>bottom</c> or <c>right</c> offset is measured from the far edge, so the
    /// position cannot be computed until the size is known.
    /// </remarks>
    static void Position(LayoutBox box, Rect containing, FontSet fonts)
    {
        var style = box.Style;
        var width = containing.Width;
        var height = containing.Height;

        var left = style.Left.ResolveOrNull(width);
        var right = style.Right.ResolveOrNull(width);
        var top = style.Top.ResolveOrNull(height);
        var bottom = style.Bottom.ResolveOrNull(height);

        // An auto margin on an absolute box resolves to zero here rather than centring it. Centring
        // is only defined when both offsets and the width are given, which is not a case this
        // distinguishes yet.
        var marginLeft = style.MarginLeft.Resolve(width);
        var marginRight = style.MarginRight.Resolve(width);
        var marginTop = style.MarginTop.Resolve(width);

        var assigned = Width(box, containing, left, right, marginLeft, marginRight, fonts);
        var used = BlockLayout.Layout(box, 0, 0, width, fonts, assigned);

        var x = left is {} fromLeft
            ? containing.X + fromLeft + marginLeft
            : right is {} fromRight
                ? containing.Right - fromRight - marginRight - box.BorderBox.Width
                : box.StaticPosition?.X ?? containing.X;

        var y = top is {} fromTop
            ? containing.Y + fromTop + marginTop
            : bottom is {} fromBottom
                ? containing.Bottom - fromBottom - style.MarginBottom.Resolve(width) - used
                : box.StaticPosition?.Y ?? containing.Y;

        box.Translate(x - box.BorderBox.X, y - box.BorderBox.Y);
    }

    /// <summary>
    /// The border-box width to give the box, or null to let the ordinary rules decide.
    /// </summary>
    /// <remarks>
    /// With both <c>left</c> and <c>right</c> given and an auto width, the box stretches to span
    /// whatever is between them — the one case where an absolute box fills its containing block
    /// rather than shrinking to its content. Otherwise it shrinks to fit, as a float does.
    /// </remarks>
    static float? Width(
        LayoutBox box,
        Rect containing,
        float? left,
        float? right,
        float marginLeft,
        float marginRight,
        FontSet fonts)
    {
        if (box.Image is not null || box.Style.Width.Kind != LengthKind.Auto)
        {
            return null;
        }

        // The room the box has is what the offsets leave of the containing block, not the whole of
        // it. CSS 2.1 §10.3.7 subtracts both offsets — treating an auto one as zero — before the
        // shrink-to-fit minimum is taken, so a box at `left: 50%` may wrap where the same content
        // at `left: 0` would not. Passing the full width instead makes every offset box one line
        // too short and as much too wide.
        var available = Math.Max(0, containing.Width - (left ?? 0) - (right ?? 0) - marginLeft - marginRight);

        if (left is not null && right is not null)
        {
            return available;
        }

        return BlockLayout.ShrinkToFit(box, available, fonts);
    }

    /// <summary>
    /// The padding box: the border box less the border widths.
    /// </summary>
    static Rect PaddingBox(LayoutBox box)
    {
        var style = box.Style;
        return box.BorderBox.Deflate(
            style.BorderTop,
            style.BorderRight,
            style.BorderBottom,
            style.BorderLeft);
    }
}
