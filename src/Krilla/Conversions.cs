/// <summary>
/// Conversions between the public types and their blittable ABI mirrors.
/// </summary>
static class Conversions
{
    internal static NativePoint ToNative(this Point value) =>
        new()
        {
            X = value.X,
            Y = value.Y
        };

    internal static NativeSize ToNative(this Size value) =>
        new()
        {
            Width = value.Width,
            Height = value.Height
        };

    internal static NativeRect ToNative(this Rectangle value) =>
        new()
        {
            Left = value.Left,
            Top = value.Top,
            Right = value.Right,
            Bottom = value.Bottom
        };

    /// <summary>
    /// The managed side is <see cref="Matrix3x2"/>, whose six components are krilla's own in the
    /// same order: <c>M11</c> and <c>M22</c> scale, <c>M12</c> and <c>M21</c> skew, and <c>M31</c>
    /// and <c>M32</c> translate. So this is a rename rather than a rearrangement, and it is the one
    /// place the two spellings meet — which is why it is written out field by field instead of
    /// reinterpreting the bytes.
    /// </summary>
    internal static NativeTransform ToNative(this Matrix3x2 value) =>
        new()
        {
            ScaleX = value.M11,
            SkewY = value.M12,
            SkewX = value.M21,
            ScaleY = value.M22,
            TranslateX = value.M31,
            TranslateY = value.M32
        };

    /// <inheritdoc cref="ToNative(Matrix3x2)"/>
    internal static Matrix3x2 ToManaged(this NativeTransform value) =>
        new(
            value.ScaleX,
            value.SkewY,
            value.SkewX,
            value.ScaleY,
            value.TranslateX,
            value.TranslateY);
}
