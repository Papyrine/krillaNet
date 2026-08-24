namespace Krilla;

/// <summary>
/// Captured drawing operations, ready to become a <see cref="Graphic"/>, a mask, or a tiling
/// pattern.
/// </summary>
/// <remarks>
/// Each use consumes the stream; a second use throws.
/// </remarks>
public sealed class ContentStream :
    IDisposable
{
    IntPtr handle;

    internal ContentStream(IntPtr handle) =>
        this.handle = handle;

    internal IntPtr Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, this);
            return handle;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        KrillaNative.krilla_stream_free(handle);
        handle = IntPtr.Zero;
    }
}