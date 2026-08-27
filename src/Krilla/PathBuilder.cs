namespace Krilla;

/// <summary>
/// Accumulates contours into a <see cref="PdfPath"/>.
/// </summary>
/// <remarks>
/// A builder produces one path. After <see cref="Build"/> it is spent, and further calls
/// throw; this mirrors krilla, whose own builder is consumed by finishing.
/// </remarks>
public sealed class PathBuilder :
    IDisposable
{
    IntPtr handle;
    bool built;

    public PathBuilder()
    {
        KrillaNative.EnsureLoaded();
        Status.Check(KrillaNative.krilla_path_builder_new(out handle), "Creating a path builder");
    }

    IntPtr Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, this);
            return handle;
        }
    }

    /// <summary>
    /// Begins a new contour.
    /// </summary>
    public PathBuilder MoveTo(Point point) => MoveTo(point.X, point.Y);

    /// <inheritdoc cref="MoveTo(Point)" />
    public PathBuilder MoveTo(float x, float y)
    {
        Status.Check(KrillaNative.krilla_path_builder_move_to(Handle, x, y), "MoveTo");
        return this;
    }

    /// <summary>
    /// Adds a straight segment from the current point.
    /// </summary>
    public PathBuilder LineTo(Point point) => LineTo(point.X, point.Y);

    /// <inheritdoc cref="LineTo(Point)" />
    public PathBuilder LineTo(float x, float y)
    {
        Status.Check(KrillaNative.krilla_path_builder_line_to(Handle, x, y), "LineTo");
        return this;
    }

    /// <summary>
    /// Adds a quadratic bezier from the current point.
    /// </summary>
    public PathBuilder QuadraticTo(float controlX, float controlY, float x, float y)
    {
        Status.Check(
            KrillaNative.krilla_path_builder_quad_to(Handle, controlX, controlY, x, y),
            "QuadraticTo");
        return this;
    }

    /// <summary>
    /// Adds a cubic bezier from the current point.
    /// </summary>
    public PathBuilder CubicTo(
        float control1X,
        float control1Y,
        float control2X,
        float control2Y,
        float x,
        float y)
    {
        Status.Check(
            KrillaNative.krilla_path_builder_cubic_to(
                Handle,
                control1X,
                control1Y,
                control2X,
                control2Y,
                x,
                y),
            "CubicTo");
        return this;
    }

    /// <summary>
    /// Closes the current contour.
    /// </summary>
    public PathBuilder Close()
    {
        Status.Check(KrillaNative.krilla_path_builder_close(Handle), "Close");
        return this;
    }

    /// <summary>
    /// Adds a complete rectangular contour.
    /// </summary>
    public PathBuilder AddRectangle(Rectangle rectangle)
    {
        Status.Check(
            KrillaNative.krilla_path_builder_push_rect(Handle, rectangle.ToNative()),
            "AddRectangle");
        return this;
    }

    /// <summary>
    /// Finishes the builder into an immutable path.
    /// </summary>
    /// <exception cref="KrillaException">
    /// The path is empty, or its geometry is degenerate.
    /// </exception>
    public PdfPath Build()
    {
        if (built)
        {
            throw new InvalidOperationException(
                "This builder has already been built. Create a new PathBuilder for each path.");
        }

        Status.Check(KrillaNative.krilla_path_builder_finish(Handle, out var path), "Building a path");
        built = true;
        return new(path);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        KrillaNative.krilla_path_builder_free(handle);
        handle = IntPtr.Zero;
    }
}