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

    internal static NativeTransform ToNative(this Matrix value) =>
        new()
        {
            ScaleX = value.ScaleX,
            SkewY = value.SkewY,
            SkewX = value.SkewX,
            ScaleY = value.ScaleY,
            TranslateX = value.TranslateX,
            TranslateY = value.TranslateY
        };

    internal static Matrix ToManaged(this NativeTransform value) =>
        new(
            value.ScaleX,
            value.SkewY,
            value.SkewX,
            value.ScaleY,
            value.TranslateX,
            value.TranslateY);
}
