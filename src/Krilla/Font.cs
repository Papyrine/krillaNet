namespace Krilla;

/// <summary>
/// A parsed OpenType or TrueType font.
/// </summary>
/// <remarks>
/// <para>
/// krilla has no font database: it does not enumerate installed fonts and does not match on
/// family or style. Fonts are supplied as bytes, and locating those bytes is the caller's
/// responsibility.
/// </para>
/// <para>
/// Creating a font is comparatively expensive and using one is cheap, so a single instance
/// should be shared across every page that needs it.
/// </para>
/// </remarks>
public sealed class Font :
    IDisposable
{
    IntPtr handle;

    Font(IntPtr handle, float unitsPerEm)
    {
        this.handle = handle;
        UnitsPerEm = unitsPerEm;
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
    /// The font's design units per em.
    /// </summary>
    /// <remarks>
    /// Needed to normalise glyph metrics when drawing a <see cref="Surface.DrawGlyphs"/>. It is the only
    /// metric krilla exposes; ascent, descent, cap height and the PostScript name are not
    /// available.
    /// </remarks>
    public float UnitsPerEm { get; }

    /// <summary>
    /// Parses a font from memory.
    /// </summary>
    /// <param name="data">The font file bytes. Copied, so the buffer need not be retained.</param>
    /// <param name="index">
    /// The face to select within a collection (<c>.ttc</c> or <c>.otc</c>). Zero for a
    /// single-font file.
    /// </param>
    /// <exception cref="KrillaException">The data could not be parsed as a font.</exception>
    public static Font Load(ReadOnlySpan<byte> data, uint index = 0)
    {
        KrillaNative.EnsureLoaded();

        Status.Check(
            KrillaNative.krilla_font_new(data, (nuint) data.Length, index, out var handle),
            "Loading a font");

        Status.Check(
            KrillaNative.krilla_font_units_per_em(handle, out var unitsPerEm),
            "Reading font units per em");

        return new(handle, unitsPerEm);
    }

    /// <summary>
    /// Parses a font from a file.
    /// </summary>
    public static Font LoadFile(string path, uint index = 0) =>
        Load(File.ReadAllBytes(path), index);

    /// <inheritdoc />
    public void Dispose()
    {
        if (handle == IntPtr.Zero)
        {
            return;
        }

        KrillaNative.krilla_font_free(handle);
        handle = IntPtr.Zero;
    }
}

/// <summary>
/// One positioned glyph in a run drawn by <see cref="Surface.DrawGlyphs"/>.
/// </summary>
/// <param name="GlyphId">The glyph index within the font.</param>
/// <param name="XAdvance">Horizontal advance, in font design units.</param>
/// <param name="TextStart">
/// Start of the UTF-16 range in the run's text that this glyph represents.
/// </param>
/// <param name="TextLength">Length of that range, in UTF-16 code units.</param>
/// <param name="XOffset">Horizontal offset, in font design units.</param>
/// <param name="YOffset">Vertical offset, in font design units.</param>
/// <param name="YAdvance">Vertical advance, in font design units.</param>
/// <remarks>
/// Metrics are given in the font's own design units and are normalised against
/// <see cref="Font.UnitsPerEm"/> when the run is drawn. krilla's own API requires
/// pre-normalised values and silently produces mis-spaced output if given raw ones; taking
/// design units here makes that mistake unreachable.
/// </remarks>
public readonly record struct Glyph(
    uint GlyphId,
    float XAdvance,
    int TextStart = 0,
    int TextLength = 0,
    float XOffset = 0f,
    float YOffset = 0f,
    float YAdvance = 0f);
