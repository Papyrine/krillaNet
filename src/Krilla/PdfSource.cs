namespace Krilla;

/// <summary>
/// An existing PDF whose pages can be reused.
/// </summary>
public sealed class PdfSource :
    IDisposable
{
    IntPtr handle;

    PdfSource(IntPtr handle, int pageCount)
    {
        this.handle = handle;
        PageCount = pageCount;
    }

    internal IntPtr Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(handle == IntPtr.Zero, this);
            return handle;
        }
    }

    /// <summary>
    /// How many pages the source document has.
    /// </summary>
    public int PageCount { get; }

    /// <summary>
    /// Parses a PDF from memory. The bytes are copied.
    /// </summary>
    /// <exception cref="KrillaException">The data is not a readable PDF.</exception>
    public static PdfSource Load(ReadOnlySpan<byte> data)
    {
        KrillaNative.EnsureLoaded();

        Status.Check(
            KrillaNative.krilla_pdf_new(data, (nuint) data.Length, out var handle),
            "Loading a PDF");

        Status.Check(KrillaNative.krilla_pdf_page_count(handle, out var count), "Reading the page count");
        return new(handle, (int) count);
    }

    /// <summary>
    /// Parses a PDF from a file.
    /// </summary>
    public static PdfSource LoadFile(string path) =>
        Load(File.ReadAllBytes(path));

    /// <inheritdoc />
    public void Dispose()
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        KrillaNative.krilla_pdf_free(handle);
        handle = IntPtr.Zero;
    }
}