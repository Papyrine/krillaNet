namespace Krilla;

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