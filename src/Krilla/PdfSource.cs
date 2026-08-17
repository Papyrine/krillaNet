namespace Krilla;

/// <summary>
/// How an attachment relates to the document it is embedded in.
/// </summary>
/// <remarks>
/// PDF/A-3 and PDF/A-4f require this to be stated meaningfully.
/// </remarks>
public enum FileAssociation
{
    /// <summary>The source the document was generated from.</summary>
    Source = 0,

    /// <summary>Data the document presents, such as the numbers behind a chart.</summary>
    Data = 1,

    /// <summary>An alternative rendition of the same content.</summary>
    Alternative = 2,

    /// <summary>Supplementary material.</summary>
    Supplement = 3,

    /// <summary>Unstated.</summary>
    Unspecified = 4
}

/// <summary>
/// How a mask's content is interpreted.
/// </summary>
public enum MaskType
{
    /// <summary>Brightness becomes opacity: white shows, black hides.</summary>
    Luminosity = 0,

    /// <summary>The mask's own alpha channel becomes opacity.</summary>
    Alpha = 1
}

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
