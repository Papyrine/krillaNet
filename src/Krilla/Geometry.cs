namespace Krilla;

/// <summary>
/// A point on a surface. The origin is the top-left corner and Y increases downward.
/// </summary>
public readonly record struct Point(float X, float Y);

/// <summary>
/// A width and height, both of which must be greater than zero.
/// </summary>
public readonly record struct Size(float Width, float Height);

/// <summary>
/// An axis-aligned rectangle.
/// </summary>
public readonly record struct Rectangle(float Left, float Top, float Right, float Bottom)
{
    /// <summary>
    /// Creates a rectangle from a corner plus a width and height.
    /// </summary>
    public static Rectangle FromSize(float x, float y, float width, float height) =>
        new(x, y, x + width, y + height);

    /// <summary>
    /// The width of the rectangle.
    /// </summary>
    public float Width => Right - Left;

    /// <summary>
    /// The height of the rectangle.
    /// </summary>
    public float Height => Bottom - Top;
}

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

/// <summary>
/// How overlapping contours are filled.
/// </summary>
public enum FillRule
{
    /// <summary>The non-zero winding rule.</summary>
    NonZero = 0,

    /// <summary>The even-odd rule.</summary>
    EvenOdd = 1
}

/// <summary>
/// How the ends of an open stroked contour are drawn.
/// </summary>
public enum LineCap
{
    /// <summary>Ends exactly at the endpoint.</summary>
    Butt = 0,

    /// <summary>Extends by a semicircle.</summary>
    Round = 1,

    /// <summary>Extends by half the stroke width.</summary>
    Square = 2
}

/// <summary>
/// How corners between stroked segments are drawn.
/// </summary>
public enum LineJoin
{
    /// <summary>Extends the outer edges until they meet.</summary>
    Miter = 0,

    /// <summary>Rounds the corner.</summary>
    Round = 1,

    /// <summary>Cuts the corner off.</summary>
    Bevel = 2
}

/// <summary>
/// How a gradient behaves outside its start and end points.
/// </summary>
public enum SpreadMethod
{
    /// <summary>Extends the terminal colours.</summary>
    Pad = 0,

    /// <summary>Mirrors the gradient repeatedly.</summary>
    Reflect = 1,

    /// <summary>Repeats the gradient from the start.</summary>
    Repeat = 2
}

/// <summary>
/// The reading direction assumed when shaping a line of text.
/// </summary>
public enum TextDirection
{
    /// <summary>Inferred from the text.</summary>
    Auto = 0,

    /// <summary>Left to right.</summary>
    LeftToRight = 1,

    /// <summary>Right to left.</summary>
    RightToLeft = 2
}
