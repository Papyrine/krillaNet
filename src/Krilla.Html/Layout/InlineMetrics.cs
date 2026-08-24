/// <summary>
/// The vertical span of an inline element's border box around a baseline.
/// </summary>
/// <remarks>
/// Shared between the painter and the box dump so the rectangle drawn and the rectangle reported
/// cannot drift apart. The font box grown by the element's own padding and border — which
/// OVERFLOWS the line box rather than growing it, CSS's rule for an inline element, and why
/// vertical padding here moves nothing on the page.
/// </remarks>
static class InlineMetrics
{
    public static (float Top, float Bottom) Extent(ComputedStyle style, FontFace face, float baseline)
    {
        var size = style.FontSize;

        return (
            baseline - face.Ascent(size) - style.PaddingTop.Resolve(0) - style.BorderTop,
            baseline + face.Descent(size) + style.PaddingBottom.Resolve(0) + style.BorderBottom);
    }

    /// <summary>The same rectangle horizontally, at <paramref name="style"/>'s own extent.</summary>
    public static Rect Reframe(Rect bounds, ComputedStyle style, FontFace face, float baseline)
    {
        var (top, bottom) = Extent(style, face, baseline);
        return new(bounds.X, top, bounds.Width, bottom - top);
    }
}