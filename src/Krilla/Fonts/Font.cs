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

    /// <summary>
    /// Shapes a line of text with the bundled shaper, returning the glyphs without drawing them.
    /// </summary>
    /// <param name="text">The text to shape.</param>
    /// <param name="direction">Reading direction.</param>
    /// <remarks>
    /// <para>
    /// What <see cref="Surface.DrawText"/> does internally, stopped one step earlier. It exists
    /// for callers that need to measure before they draw — a layout engine cannot decide where a
    /// line breaks without knowing how wide a word is, and the sum of a font's raw advances is
    /// not that width: it misses kerning and every ligature.
    /// </para>
    /// <para>
    /// The result feeds <see cref="Surface.DrawGlyphs"/> unchanged, so a run can be measured and
    /// then drawn from the same numbers. Same limits as <see cref="Surface.DrawText"/>: one font,
    /// one script, no bidirectional resolution and no fallback.
    /// </para>
    /// </remarks>
    public Glyph[] Shape(string text, TextDirection direction = TextDirection.Auto)
    {
        if (text.Length == 0)
        {
            return [];
        }

        var utf8 = Encoding.UTF8.GetBytes(text);

        Status.Check(
            KrillaNative.krilla_font_shape(
                Handle,
                utf8,
                (nuint) utf8.Length,
                (int) direction,
                out var pointer,
                out var count),
            "Shaping text");

        try
        {
            return ReadGlyphs(pointer, (int) count, text, utf8);
        }
        finally
        {
            // R2: the native side allocated it, so the native side frees it. The Windows builds
            // link the CRT statically and so do not share the host's allocator; freeing this from
            // managed code corrupts the heap rather than failing.
            KrillaNative.krilla_glyphs_free(pointer, count);
        }
    }

    /// <summary>
    /// Converts a native glyph run into the units this API uses everywhere else.
    /// </summary>
    /// <remarks>
    /// Two conversions, both inward-facing. Advances arrive divided by units-per-em and are
    /// multiplied back, because <see cref="Glyph"/> is documented in design units and
    /// <see cref="Surface.DrawGlyphs"/> divides again on the way out. Text offsets arrive as UTF-8
    /// byte positions and become UTF-16 indices, because that is what a caller holding a
    /// <see cref="string"/> can actually use.
    /// </remarks>
    unsafe Glyph[] ReadGlyphs(IntPtr pointer, int count, string text, byte[] utf8)
    {
        if (count == 0 || pointer == IntPtr.Zero)
        {
            return [];
        }

        var offsets = Utf8ToUtf16Offsets(text, utf8.Length);
        var native = (NativeGlyph*) pointer;
        var glyphs = new Glyph[count];

        for (var index = 0; index < count; index++)
        {
            var glyph = native[index];
            var start = offsets[Math.Clamp((int) glyph.TextStart, 0, utf8.Length)];
            var end = offsets[Math.Clamp((int) glyph.TextEnd, 0, utf8.Length)];

            glyphs[index] = new(
                glyph.GlyphId,
                glyph.XAdvance * UnitsPerEm,
                start,
                Math.Max(0, end - start),
                glyph.XOffset * UnitsPerEm,
                glyph.YOffset * UnitsPerEm,
                glyph.YAdvance * UnitsPerEm);
        }

        return glyphs;
    }

    /// <summary>
    /// Maps every UTF-8 byte offset in <paramref name="text"/> to its UTF-16 index.
    /// </summary>
    /// <remarks>
    /// The inverse of what <see cref="Surface.DrawGlyphs"/> builds. Every byte of a multi-byte
    /// sequence maps to the index of the character it belongs to, so an offset landing mid-
    /// character still resolves to something sensible rather than to the next character.
    /// </remarks>
    static int[] Utf8ToUtf16Offsets(string text, int byteLength)
    {
        var offsets = new int[byteLength + 1];
        var byteIndex = 0;

        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            var isPair = char.IsHighSurrogate(current) &&
                         index + 1 < text.Length &&
                         char.IsLowSurrogate(text[index + 1]);

            var bytes = isPair
                ? 4
                : current switch
                {
                    < (char) 0x80 => 1,
                    < (char) 0x800 => 2,
                    _ => 3
                };

            for (var offset = 0; offset < bytes && byteIndex < offsets.Length; offset++)
            {
                offsets[byteIndex++] = index;
            }

            if (isPair)
            {
                index++;
            }
        }

        offsets[byteLength] = text.Length;
        return offsets;
    }

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