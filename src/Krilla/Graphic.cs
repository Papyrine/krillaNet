namespace Krilla;

/// <summary>
/// A drawing captured once and reused, at negligible cost in file size.
/// </summary>
/// <remarks>
/// Belongs to the document that created it. Drawing it into another throws rather than
/// producing a PDF that references objects which do not exist — a mistake krilla cannot
/// detect on its own.
/// </remarks>
public sealed class Graphic :
    IDisposable
{
    IntPtr handle;

    internal Graphic(IntPtr handle) =>
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

        KrillaNative.krilla_graphic_free(handle);
        handle = IntPtr.Zero;
    }
}