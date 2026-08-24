namespace Krilla;

/// <summary>
/// What a fill or stroke paints with: a solid colour, or one of the three gradient kinds.
/// </summary>
/// <remarks>
/// A paint is independent of any document and can be reused freely across pages and
/// documents.
/// </remarks>
public sealed class Paint :
    IDisposable
{
    IntPtr handle;

    Paint(IntPtr handle) =>
        this.handle = handle;

    /// <summary>
    /// Wraps a paint handle produced elsewhere in the assembly, such as a tiling pattern built
    /// from a captured stream.
    /// </summary>
    internal static Paint FromHandle(IntPtr handle) =>
        new(handle);

    internal IntPtr Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, this);
            return handle;
        }
    }

    /// <summary>
    /// Creates a solid-colour paint.
    /// </summary>
    public static Paint Solid(Color color)
    {
        KrillaNative.EnsureLoaded();
        Status.Check(
            KrillaNative.krilla_paint_new_color(color.ToNative(), out var handle),
            "Creating a solid paint");
        return new(handle);
    }

    /// <summary>
    /// Creates a linear gradient running from (<paramref name="x1"/>, <paramref name="y1"/>)
    /// to (<paramref name="x2"/>, <paramref name="y2"/>).
    /// </summary>
    /// <remarks>
    /// Every stop must use the same colour space. Mixing, say, <see cref="Color.White"/>
    /// (greyscale) with an RGB stop throws.
    /// </remarks>
    public static Paint LinearGradient(
        float x1,
        float y1,
        float x2,
        float y2,
        IReadOnlyList<GradientStop> stops,
        SpreadMethod spread = SpreadMethod.Pad,
        Matrix? transform = null,
        bool antiAlias = true)
    {
        KrillaNative.EnsureLoaded();
        var native = ToNative(stops);

        Status.Check(
            KrillaNative.krilla_paint_new_linear_gradient(
                x1,
                y1,
                x2,
                y2,
                (transform ?? Matrix.Identity).ToNative(),
                (int) spread,
                antiAlias,
                native,
                (nuint) native.Length,
                out var handle),
            "Creating a linear gradient");

        return new(handle);
    }

    /// <summary>
    /// Creates a radial gradient between a start circle and an end circle.
    /// </summary>
    /// <remarks>
    /// krilla does not implement <see cref="SpreadMethod.Reflect"/> or
    /// <see cref="SpreadMethod.Repeat"/> for radial gradients and silently falls back to
    /// <see cref="SpreadMethod.Pad"/>.
    /// </remarks>
    public static Paint RadialGradient(
        float startX,
        float startY,
        float startRadius,
        float endX,
        float endY,
        float endRadius,
        IReadOnlyList<GradientStop> stops,
        SpreadMethod spread = SpreadMethod.Pad,
        Matrix? transform = null,
        bool antiAlias = true)
    {
        KrillaNative.EnsureLoaded();
        var native = ToNative(stops);

        Status.Check(
            KrillaNative.krilla_paint_new_radial_gradient(
                startX,
                startY,
                startRadius,
                endX,
                endY,
                endRadius,
                (transform ?? Matrix.Identity).ToNative(),
                (int) spread,
                antiAlias,
                native,
                (nuint) native.Length,
                out var handle),
            "Creating a radial gradient");

        return new(handle);
    }

    /// <summary>
    /// Creates a sweep gradient about a centre point.
    /// </summary>
    /// <remarks>
    /// Angles are in degrees, starting from the right and increasing counter-clockwise.
    /// </remarks>
    public static Paint SweepGradient(
        float centerX,
        float centerY,
        float startAngle,
        float endAngle,
        IReadOnlyList<GradientStop> stops,
        SpreadMethod spread = SpreadMethod.Pad,
        Matrix? transform = null,
        bool antiAlias = true)
    {
        KrillaNative.EnsureLoaded();
        var native = ToNative(stops);

        Status.Check(
            KrillaNative.krilla_paint_new_sweep_gradient(
                centerX,
                centerY,
                startAngle,
                endAngle,
                (transform ?? Matrix.Identity).ToNative(),
                (int) spread,
                antiAlias,
                native,
                (nuint) native.Length,
                out var handle),
            "Creating a sweep gradient");

        return new(handle);
    }

    static NativeStop[] ToNative(IReadOnlyList<GradientStop> stops)
    {
        if (stops.Count == 0)
        {
            throw new ArgumentException("A gradient needs at least one stop.", nameof(stops));
        }

        var native = new NativeStop[stops.Count];

        for (var index = 0; index < stops.Count; index++)
        {
            var stop = stops[index];
            native[index] = new()
            {
                Offset = stop.Offset,
                Color = stop.Color.ToNative(),
                Opacity = stop.Opacity
            };
        }

        return native;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        KrillaNative.krilla_paint_free(handle);
        handle = IntPtr.Zero;
    }
}