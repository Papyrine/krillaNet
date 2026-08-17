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
