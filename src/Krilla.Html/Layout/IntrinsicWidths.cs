/// <summary>
/// How narrow and how wide a box wants to be, without laying it out.
/// </summary>
/// <remarks>
/// <para>
/// The min-content width is the widest thing that cannot be broken — the longest word, in
/// practice. The max-content width is what the content would occupy given unlimited room, so no
/// wrapping at all. Every other width a box can take sits between them.
/// </para>
/// <para>
/// Only tables need these, and they need them badly: a column's width is decided before any cell
/// is laid out, so the decision has to be made from measurements rather than from results. That
/// ordering is the whole reason this exists as a separate pass rather than falling out of layout.
/// </para>
/// <para>
/// Percentages resolve to zero here, because the containing block they resolve against is exactly
/// what is being computed. That is the conventional answer — a percentage contributes nothing to
/// an intrinsic size — rather than an approximation.
/// </para>
/// </remarks>
static class IntrinsicWidths
{
    /// <summary>
    /// The min-content and max-content widths of <paramref name="box"/>'s BORDER box.
    /// </summary>
    /// <remarks>
    /// Border box rather than content box, because every caller is deciding how much room the box
    /// needs on a line or in a column, and the border and padding occupy that room too.
    /// </remarks>
    public static (float Min, float Max) Measure(LayoutBox box, FontSet fonts)
    {
        var style = box.Style;
        // Percentages resolve to zero here, for the reason the class header gives.
        var surround = style.SurroundX(0);

        // A definite width settles both: the box wants exactly that, whatever is inside it. Under
        // `border-box` the declaration already covers the surround, so a cell declaring 100px with
        // 10px of padding wants a 100px column rather than a 120px one.
        if (style.Width.Kind == LengthKind.Absolute)
        {
            var declared = style.ContentSize(style.Width.Value, surround) + surround;
            return (declared, declared);
        }

        var (min, max) = Content(box, fonts);

        // A max-width caps the preferred size but cannot force content to be narrower than it can
        // possibly be, which is why only the maximum is clamped.
        if (style.MaxWidth.Kind == LengthKind.Absolute)
        {
            max = Math.Min(max, style.ContentSize(style.MaxWidth.Value, surround));
            min = Math.Min(min, max);
        }

        if (style.MinWidth.Kind == LengthKind.Absolute)
        {
            min = Math.Max(min, style.ContentSize(style.MinWidth.Value, surround));
            max = Math.Max(max, min);
        }

        return (min + surround, max + surround);
    }

    /// <summary>
    /// The intrinsic widths of what is inside <paramref name="box"/>, excluding its own box model.
    /// </summary>
    static (float Min, float Max) Content(LayoutBox box, FontSet fonts)
    {
        // A nested table decides its own widths by its own algorithm, so it answers for itself
        // rather than being treated as a block full of rows.
        if (box.Style.Display == DisplayKind.Table)
        {
            return TableLayout.Intrinsic(box, fonts);
        }

        if (box.Image is {} image)
        {
            var width = ReplacedSizing.Resolve(box.Style, image, 0, box.Style.SurroundX(0)).Width;
            return (width, width);
        }

        if (box.IsInlineContainer)
        {
            var (textMin, textMax) = InlineLayout.Intrinsic(box.Inlines, fonts);

            // The first line starts that far in, so both widths need room for it or a column sized
            // from them wraps a cell that was supposed to fit. A hanging indent asks for no extra
            // room, and a percentage resolves to zero for the reason the class header gives.
            var indent = box.Style.TextIndent.Kind == LengthKind.Absolute
                ? Math.Max(0, box.Style.TextIndent.Value)
                : 0;

            return (textMin + indent, textMax + indent);
        }

        var min = 0f;
        var max = 0f;

        foreach (var child in box.Children)
        {
            var (childMin, childMax) = Measure(child, fonts);

            // Horizontal margins hold a child away from the edges, so they are part of how much
            // room it needs — unlike the vertical ones, which collapse and belong to layout.
            var margins =
                child.Style.MarginLeft.Resolve(0) +
                child.Style.MarginRight.Resolve(0);

            min = Math.Max(min, childMin + margins);
            max = Math.Max(max, childMax + margins);
        }

        return (min, max);
    }
}
