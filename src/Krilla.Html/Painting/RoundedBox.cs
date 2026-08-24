/// <summary>
/// The rounded rectangle <c>border-radius</c> asks for, resolved and drawable.
/// </summary>
/// <param name="TopLeft">Horizontal and vertical radii of the top-left corner.</param>
/// <param name="TopRight">Horizontal and vertical radii of the top-right corner.</param>
/// <param name="BottomRight">Horizontal and vertical radii of the bottom-right corner.</param>
/// <param name="BottomLeft">Horizontal and vertical radii of the bottom-left corner.</param>
/// <remarks>
/// <para>
/// Each corner carries two radii rather than one, because <c>border-radius: 30px / 12px</c> is an
/// ellipse quadrant rather than a circular one. Treating a corner as a single radius is right for
/// almost every document and wrong the moment anyone writes the slash form, and the two-radius
/// shape costs nothing over the one-radius shape since the arcs are drawn as beziers either way.
/// </para>
/// <para>
/// Resolved against a box rather than stored as declared: a percentage radius resolves against the
/// box's own width horizontally and its own height vertically, and the overlap clamp below needs
/// real lengths to compare.
/// </para>
/// </remarks>
readonly record struct RoundedBox(
    (float X, float Y) TopLeft,
    (float X, float Y) TopRight,
    (float X, float Y) BottomRight,
    (float X, float Y) BottomLeft)
{
    /// <summary>
    /// How far a cubic bezier control point sits along the tangent to approximate a quarter arc.
    /// </summary>
    /// <remarks>
    /// The standard circle constant, 4/3 × (√2 − 1). It is what every renderer uses to draw a
    /// circle out of four beziers, browsers included — which is the reason to use it here rather
    /// than any closer approximation: the corpus compares against Chrome's own output, so matching
    /// its construction matters more than matching a true arc.
    /// </remarks>
    const float kappa = 0.5522847498f;

    /// <summary>Whether any corner is actually rounded.</summary>
    public bool IsRounded =>
        TopLeft.X > 0 || TopLeft.Y > 0 ||
        TopRight.X > 0 || TopRight.Y > 0 ||
        BottomRight.X > 0 || BottomRight.Y > 0 ||
        BottomLeft.X > 0 || BottomLeft.Y > 0;

    /// <summary>
    /// The radii <paramref name="style"/> declares for a box of <paramref name="size"/>, scaled
    /// back where neighbouring corners would overlap.
    /// </summary>
    /// <remarks>
    /// CSS Backgrounds 3 §5.5: when two radii on the same side add up to more than that side's
    /// length, every radius on the box is scaled by the same factor — the smallest that makes
    /// every side fit. Scaling one side alone would distort the shape, and is the mistake
    /// <c>border-radius: 999px</c> reveals: on a box wider than it is tall, the correct answer is
    /// a pill and the incorrect one is a rectangle with two circular ends of different sizes.
    /// </remarks>
    public static RoundedBox Resolve(ComputedStyle style, Rect size)
    {
        var box = new RoundedBox(
            Corner(style.RadiusTopLeft, size),
            Corner(style.RadiusTopRight, size),
            Corner(style.RadiusBottomRight, size),
            Corner(style.RadiusBottomLeft, size));

        var scale = MathF.Min(
            MathF.Min(
                Ratio(size.Width, box.TopLeft.X + box.TopRight.X),
                Ratio(size.Width, box.BottomLeft.X + box.BottomRight.X)),
            MathF.Min(
                Ratio(size.Height, box.TopLeft.Y + box.BottomLeft.Y),
                Ratio(size.Height, box.TopRight.Y + box.BottomRight.Y)));

        if (scale >= 1)
        {
            return box;
        }

        return box.Scaled(scale);

        static (float X, float Y) Corner((CssLength X, CssLength Y) radius, Rect size) =>
            (MathF.Max(0, radius.X.Resolve(size.Width)),
             MathF.Max(0, radius.Y.Resolve(size.Height)));

        static float Ratio(float side, float sum)
        {
            if (sum <= 0)
            {
                return float.PositiveInfinity;
            }

            return side / sum;
        }
    }

    /// <summary>
    /// This shape inset by the four border widths, which is the inner edge of a rounded border.
    /// </summary>
    /// <remarks>
    /// Each radius shrinks by the width of the edge it runs along, and floors at zero — so a
    /// corner whose radius is smaller than the border around it comes to a square inner corner,
    /// which is what a browser draws and what makes a thick rounded border look like a ring rather
    /// than a tube.
    /// </remarks>
    public RoundedBox Deflate(float top, float right, float bottom, float left) =>
        new(
            (MathF.Max(0, TopLeft.X - left), MathF.Max(0, TopLeft.Y - top)),
            (MathF.Max(0, TopRight.X - right), MathF.Max(0, TopRight.Y - top)),
            (MathF.Max(0, BottomRight.X - right), MathF.Max(0, BottomRight.Y - bottom)),
            (MathF.Max(0, BottomLeft.X - left), MathF.Max(0, BottomLeft.Y - bottom)));

    RoundedBox Scaled(float factor) =>
        new(
            (TopLeft.X * factor, TopLeft.Y * factor),
            (TopRight.X * factor, TopRight.Y * factor),
            (BottomRight.X * factor, BottomRight.Y * factor),
            (BottomLeft.X * factor, BottomLeft.Y * factor));

    /// <summary>
    /// Traces this shape around <paramref name="rect"/> into <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The path being built.</param>
    /// <param name="rect">The rectangle the corners are cut from.</param>
    /// <param name="clockwise">
    /// Which way round to wind. A ring is an outer contour one way and an inner contour the other,
    /// so the non-zero fill rule cuts the middle out.
    /// </param>
    public void Trace(PathBuilder builder, Rect rect, bool clockwise)
    {
        var (left, top, right, bottom) = (rect.X, rect.Y, rect.Right, rect.Bottom);

        if (clockwise)
        {
            builder.MoveTo(left + TopLeft.X, top);
            builder.LineTo(right - TopRight.X, top);
            Arc(right - TopRight.X, top, right, top + TopRight.Y, TopRight, horizontalFirst: true);
            builder.LineTo(right, bottom - BottomRight.Y);
            Arc(right, bottom - BottomRight.Y, right - BottomRight.X, bottom, BottomRight, horizontalFirst: false);
            builder.LineTo(left + BottomLeft.X, bottom);
            Arc(left + BottomLeft.X, bottom, left, bottom - BottomLeft.Y, BottomLeft, horizontalFirst: true);
            builder.LineTo(left, top + TopLeft.Y);
            Arc(left, top + TopLeft.Y, left + TopLeft.X, top, TopLeft, horizontalFirst: false);
        }
        else
        {
            builder.MoveTo(left + TopLeft.X, top);
            Arc(left + TopLeft.X, top, left, top + TopLeft.Y, TopLeft, horizontalFirst: true);
            builder.LineTo(left, bottom - BottomLeft.Y);
            Arc(left, bottom - BottomLeft.Y, left + BottomLeft.X, bottom, BottomLeft, horizontalFirst: false);
            builder.LineTo(right - BottomRight.X, bottom);
            Arc(right - BottomRight.X, bottom, right, bottom - BottomRight.Y, BottomRight, horizontalFirst: true);
            builder.LineTo(right, top + TopRight.Y);
            Arc(right, top + TopRight.Y, right - TopRight.X, top, TopRight, horizontalFirst: false);
        }

        builder.Close();

        // One quarter of an ellipse, from wherever the pen is to (endX, endY). The control points
        // sit `kappa` of the way along each tangent, which is the same construction a browser uses.
        void Arc(float startX, float startY, float endX, float endY, (float X, float Y) radius, bool horizontalFirst)
        {
            if (radius.X <= 0 || radius.Y <= 0)
            {
                builder.LineTo(endX, endY);
                return;
            }

            var (c1x, c1y) = horizontalFirst
                ? (startX + (endX - startX) * kappa, startY)
                : (startX, startY + (endY - startY) * kappa);

            var (c2x, c2y) = horizontalFirst
                ? (endX, endY + (startY - endY) * kappa)
                : (endX + (startX - endX) * kappa, endY);

            builder.CubicTo(c1x, c1y, c2x, c2y, endX, endY);
        }
    }
}
