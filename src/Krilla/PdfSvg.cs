namespace Krilla;

/// <summary>
/// A parsed SVG document, drawable into any rectangle.
/// </summary>
/// <remarks>
/// <para>
/// Parsing is the expensive half and drawing is cheap, so one instance should be shared
/// everywhere the same graphic appears — a logo repeated on every page is parsed once.
/// </para>
/// <para>
/// An <c>&lt;image&gt;</c> inside the SVG is honoured only when its <c>href</c> is a
/// <c>data:</c> URI. One naming a file or a URL resolves to nothing, deliberately: an SVG is
/// content, frequently from somewhere untrusted, and usvg's stock behaviour is to read the path
/// off disk relative to the working directory and embed what it finds. A data URI's bytes are
/// already in the document, so admitting them grants nothing.
/// </para>
/// </remarks>
public sealed class PdfSvg :
    IDisposable
{
    IntPtr handle;

    PdfSvg(IntPtr handle, float width, float height)
    {
        this.handle = handle;
        Width = width;
        Height = height;
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
    /// Intrinsic width, in CSS pixels. Always positive.
    /// </summary>
    /// <remarks>
    /// The resolved size rather than the raw <c>width</c> attribute: a document giving only a
    /// <c>viewBox</c> reports the viewBox's extent, and one giving neither reports usvg's
    /// 100x100 default. Positive in every case, so <see cref="Width"/> over <see cref="Height"/>
    /// is always a usable aspect ratio.
    /// </remarks>
    public float Width { get; }

    /// <summary>
    /// Intrinsic height, in CSS pixels. Always positive.
    /// </summary>
    public float Height { get; }

    /// <summary>
    /// Parses an SVG document, plain or gzip-compressed.
    /// </summary>
    /// <param name="data">The bytes. Copied, so the buffer need not be retained.</param>
    /// <param name="options">
    /// The fonts text inside the SVG resolves against, or null for none — correct unless the
    /// document carries <c>&lt;text&gt;</c>.
    /// </param>
    /// <exception cref="KrillaException">The data is not a parseable SVG document.</exception>
    public static PdfSvg Load(ReadOnlySpan<byte> data, SvgOptions? options = null)
    {
        KrillaNative.EnsureLoaded();
        EnsureSupported();

        Status.Check(
            KrillaNative.krilla_svg_new(
                data,
                (nuint) data.Length,
                options?.Handle ?? IntPtr.Zero,
                out var handle),
            "Parsing an SVG");

        Status.Check(
            KrillaNative.krilla_svg_size(handle, out var width, out var height),
            "Reading SVG size");

        return new(handle, width, height);
    }

    /// <summary>
    /// Parses an SVG document from a file.
    /// </summary>
    public static PdfSvg LoadFile(string path, SvgOptions? options = null) =>
        Load(File.ReadAllBytes(path), options);

    /// <summary>
    /// Whether the loaded native library was built with SVG support.
    /// </summary>
    /// <remarks>
    /// True for every published package. False only against a native built by hand with
    /// <c>--no-default-features</c>, which is the case this exists to name.
    /// </remarks>
    public static bool IsSupported
    {
        get
        {
            KrillaNative.EnsureLoaded();
            return KrillaNative.SvgSupported;
        }
    }

    internal static void EnsureSupported()
    {
        if (!KrillaNative.SvgSupported)
        {
            throw new KrillaException(
                "The native krilla library was built without SVG support. Rebuild it with the 'svg' cargo feature, which is on by default and off only for a hand-built native.");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        KrillaNative.krilla_svg_free(handle);
        handle = IntPtr.Zero;
    }
}
