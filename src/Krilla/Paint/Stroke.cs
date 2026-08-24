namespace Krilla;

/// <summary>
/// How a shape's outline is painted.
/// </summary>
/// <param name="Paint">The paint to use.</param>
/// <param name="Width">Stroke width in surface units.</param>
/// <param name="Opacity">Opacity from 0 to 1.</param>
/// <param name="LineCap">How open ends are drawn.</param>
/// <param name="LineJoin">How corners are drawn.</param>
/// <param name="MiterLimit">Ratio at which a miter join degrades to a bevel.</param>
/// <param name="DashArray">Alternating on/off lengths, or null for a solid stroke.</param>
/// <param name="DashOffset">How far into the dash pattern to start.</param>
public readonly record struct Stroke(
    Paint Paint,
    float Width = 1f,
    float Opacity = 1f,
    LineCap LineCap = LineCap.Butt,
    LineJoin LineJoin = LineJoin.Miter,
    float MiterLimit = 10f,
    float[]? DashArray = null,
    float DashOffset = 0f)
{
    /// <summary>
    /// Wraps a paint in a solid, one-unit-wide stroke.
    /// </summary>
    public static implicit operator Stroke(Paint paint) =>
        new(paint);
}