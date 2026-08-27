namespace Krilla;

/// <summary>
/// The fonts an SVG's <c>&lt;text&gt;</c> is shaped against, and the family it falls back to.
/// </summary>
/// <remarks>
/// <para>
/// Only needed for an SVG that carries text; <see cref="PdfSvg.Load(ReadOnlySpan{byte},SvgOptions)"/>
/// accepts null, which is right for the great majority of them. Reusable across any number of
/// documents, and worth reusing: the font database is rebuilt from scratch otherwise.
/// </para>
/// <para>
/// Settings apply when an SVG is parsed rather than when it is drawn, because usvg resolves
/// text as it parses — a <c>&lt;text&gt;</c> element becomes positioned glyphs in the tree, not
/// a node still carrying a family name. Fonts registered after a <see cref="PdfSvg"/> exists
/// reach nothing in it.
/// </para>
/// <para>
/// Nothing is loaded from the host. Which fonts an SVG can use is the caller's decision, for
/// the same reason it is for the document around it, and for the same reason krilla has no font
/// database of its own: a conversion that reaches for whatever the machine happens to have
/// installed stops being reproducible.
/// </para>
/// </remarks>
public sealed class SvgOptions :
    IDisposable
{
    IntPtr handle;

    public SvgOptions()
    {
        KrillaNative.EnsureLoaded();
        PdfSvg.EnsureSupported();

        Status.Check(
            KrillaNative.krilla_svg_options_new(out handle),
            "Creating SVG options");
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
    /// Registers a font for text inside an SVG parsed with these options.
    /// </summary>
    /// <param name="data">The font bytes. Copied, so the buffer need not be retained.</param>
    public SvgOptions AddFont(ReadOnlySpan<byte> data)
    {
        Status.Check(
            KrillaNative.krilla_svg_options_add_font(Handle, data, (nuint) data.Length),
            "Registering an SVG font");

        return this;
    }

    /// <summary>
    /// Registers a font from a file.
    /// </summary>
    public SvgOptions AddFontFile(string path) =>
        AddFont(File.ReadAllBytes(path));

    /// <summary>
    /// Sets the family used for text that names no family, or names one that was not registered.
    /// </summary>
    /// <remarks>
    /// usvg's own default is <c>Times New Roman</c>, which against a database holding only the
    /// fonts registered here resolves to nothing at all — so an SVG whose text names no family
    /// draws no text unless this names one that was.
    /// </remarks>
    public SvgOptions SetDefaultFamily(string family)
    {
        var utf8 = Encoding.UTF8.GetBytes(family);

        Status.Check(
            KrillaNative.krilla_svg_options_set_default_family(Handle, utf8, (nuint) utf8.Length),
            "Setting the default SVG font family");

        return this;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        KrillaNative.krilla_svg_options_free(handle);
        handle = IntPtr.Zero;
    }
}
