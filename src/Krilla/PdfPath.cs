namespace Krilla;

/// <summary>
/// An immutable path, built with <see cref="PathBuilder"/>.
/// </summary>
/// <remarks>
/// A path is independent of any document and can be drawn into as many as needed.
/// </remarks>
public sealed class PdfPath :
    IDisposable
{
    IntPtr handle;

    internal PdfPath(IntPtr handle) =>
        this.handle = handle;

    internal IntPtr Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, this);
            return handle;
        }
    }

    /// <summary>
    /// Builds a path covering a rectangle.
    /// </summary>
    public static PdfPath Rectangle(Rectangle rectangle)
    {
        using var builder = new PathBuilder();
        builder.AddRectangle(rectangle);
        return builder.Build();
    }

    /// <summary>
    /// Builds a closed polygon through the given points.
    /// </summary>
    public static PdfPath Polygon(params ReadOnlySpan<Point> points)
    {
        if (points.Length < 2)
        {
            throw new ArgumentException("A polygon needs at least two points.", nameof(points));
        }

        using var builder = new PathBuilder();
        builder.MoveTo(points[0]);

        for (var index = 1; index < points.Length; index++)
        {
            builder.LineTo(points[index]);
        }

        builder.Close();
        return builder.Build();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        KrillaNative.krilla_path_free(handle);
        handle = IntPtr.Zero;
    }
}