namespace Krilla;

/// <summary>
/// An affine transformation matrix.
/// </summary>
/// <remarks>
/// Unlike the other geometry types this one is never validated: krilla inherits Skia's
/// tolerance of degenerate and non-finite matrices.
/// </remarks>
public readonly record struct Matrix(
    float ScaleX,
    float SkewY,
    float SkewX,
    float ScaleY,
    float TranslateX,
    float TranslateY)
{
    /// <summary>
    /// The identity matrix.
    /// </summary>
    public static Matrix Identity => new(1, 0, 0, 1, 0, 0);

    /// <summary>
    /// Creates a translation.
    /// </summary>
    public static Matrix Translate(float x, float y) => new(1, 0, 0, 1, x, y);

    /// <summary>
    /// Creates a scale.
    /// </summary>
    public static Matrix Scale(float x, float y) => new(x, 0, 0, y, 0, 0);

    /// <summary>
    /// Creates a rotation, in degrees, about the origin.
    /// </summary>
    public static Matrix Rotate(float degrees)
    {
        var radians = degrees * (float) Math.PI / 180f;
        var cos = (float) Math.Cos(radians);
        var sin = (float) Math.Sin(radians);
        return new(cos, sin, -sin, cos, 0, 0);
    }
}