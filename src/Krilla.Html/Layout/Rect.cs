/// <summary>A rectangle in CSS pixels, with the origin at the top-left of the page.</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
readonly record struct Rect(float X, float Y, float Width, float Height)
{
    /// <summary>Right edge.</summary>
    public float Right => X + Width;

    /// <summary>Bottom edge.</summary>
    public float Bottom => Y + Height;

    /// <summary>This rectangle inset by the given edge widths.</summary>
    public Rect Deflate(float top, float right, float bottom, float left) =>
        new(X + left, Y + top, Math.Max(0, Width - left - right), Math.Max(0, Height - top - bottom));

    /// <summary>This rectangle moved by the given offset.</summary>
    public Rect Offset(float dx, float dy) =>
        new(X + dx, Y + dy, Width, Height);
}