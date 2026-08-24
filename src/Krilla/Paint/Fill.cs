namespace Krilla;

/// <summary>
/// How a shape's interior is painted.
/// </summary>
/// <param name="Paint">The paint to use.</param>
/// <param name="Opacity">Opacity from 0 to 1.</param>
/// <param name="Rule">Which parts of overlapping contours count as inside.</param>
public readonly record struct Fill(
    Paint Paint,
    float Opacity = 1f,
    FillRule Rule = FillRule.NonZero)
{
    /// <summary>
    /// Wraps a paint in a fully opaque, non-zero fill.
    /// </summary>
    /// <remarks>
    /// Lets <c>SetFill(paint)</c> read naturally. Without it the call has to be written
    /// <c>SetFill(new Fill(paint))</c>, since a target-typed <c>new(paint)</c> is ambiguous
    /// against the colour overload.
    /// </remarks>
    public static implicit operator Fill(Paint paint) =>
        new(paint);
}