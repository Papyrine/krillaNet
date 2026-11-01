using System.Globalization;

namespace Krilla;

/// <summary>
/// A parsed OpenType or TrueType font.
/// </summary>
/// <remarks>
/// <para>
/// krilla has no font database: it does not enumerate installed fonts and does not match on
/// family or style. Fonts are supplied as bytes — <see cref="Load"/> and <see cref="LoadFile"/> —
/// or found by family and style with <see cref="Find"/>, which searches a
/// <see cref="FontRepository"/> scan written on this side.
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
    /// Finds and loads an installed font by family name and style, or returns null when nothing
    /// matches.
    /// </summary>
    /// <param name="family">
    /// A family name such as <c>Arial</c>, a PDF Standard-14 name such as <c>Helvetica-Bold</c>,
    /// or a full or PostScript name such as <c>Arial Bold</c>.
    /// </param>
    /// <param name="bold">Prefer a bold face.</param>
    /// <param name="italic">Prefer an italic or oblique face.</param>
    /// <remarks>
    /// Searches <see cref="FontRepository.Default"/>, which is the installed fonts unless a
    /// caller has replaced it; see <see cref="FontRepository"/> for how a request is matched.
    /// Each call loads a new font, which the caller owns.
    /// </remarks>
    public static Font? Find(string family, bool bold = false, bool italic = false) =>
        FontRepository.Default.Find(family, bold, italic);

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
    /// The advance width of <paramref name="text"/> drawn as one line at
    /// <paramref name="fontSize"/>, in the same units as the size.
    /// </summary>
    /// <remarks>
    /// The width <see cref="Surface.DrawText"/> would cover: shaped, so kerning and ligatures
    /// are in it, which a sum of raw advances would miss.
    /// </remarks>
    public float MeasureText(string text, float fontSize)
    {
        float advance = 0;
        foreach (var glyph in Shape(text))
        {
            advance += glyph.XAdvance;
        }

        return advance / UnitsPerEm * fontSize;
    }

    /// <summary>
    /// Breaks <paramref name="text"/> into lines no wider than <paramref name="maxWidth"/> at
    /// <paramref name="fontSize"/>, for drawing one by one with <see cref="Surface.DrawText"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Breaks at spaces, and inside a word only when the word alone is wider than the line — and
    /// then between characters, never inside a surrogate pair or a combining sequence. A line
    /// break in the text (<c>\n</c>, <c>\r\n</c> or <c>\r</c>) always starts a new line, so an
    /// empty line in the text is an empty string in the result. Runs of spaces collapse to one,
    /// and none is left at either end of a line.
    /// </para>
    /// <para>
    /// Each word is shaped once and a line is measured as its words plus the spaces between
    /// them, so kerning across a space is not counted. That is a fraction of a point in any font
    /// that kerns spaces at all, and it keeps wrapping a paragraph linear in its length rather
    /// than quadratic.
    /// </para>
    /// <para>
    /// The result always has at least one line, which is empty for empty text. A single
    /// character wider than <paramref name="maxWidth"/> still gets a line of its own, so the
    /// result can exceed the width only there.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxWidth"/> is not positive.</exception>
    public IReadOnlyList<string> WrapText(string text, float fontSize, float maxWidth)
    {
        if (!(maxWidth > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(maxWidth), maxWidth, "The width must be positive.");
        }

        var lines = new List<string>();
        var space = MeasureText(" ", fontSize);

        foreach (var paragraph in text.ReplaceLineEndings("\n").Split('\n'))
        {
            WrapParagraph(paragraph, fontSize, maxWidth, space, lines);
        }

        return lines;
    }

    void WrapParagraph(string paragraph, float fontSize, float maxWidth, float space, List<string> lines)
    {
        var line = new StringBuilder();
        float lineWidth = 0;

        foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var width = MeasureText(word, fontSize);

            if (line.Length > 0)
            {
                if (lineWidth + space + width <= maxWidth)
                {
                    line.Append(' ').Append(word);
                    lineWidth += space + width;
                    continue;
                }

                lines.Add(line.ToString());
                line.Clear();
            }

            // The word starts a line. Only now, with nothing else on it, can the word be too
            // wide for any line, and only then is it broken.
            var rest = word;
            while (width > maxWidth)
            {
                var fits = FittingPrefix(rest, fontSize, maxWidth);
                if (fits == rest.Length)
                {
                    break;
                }

                lines.Add(rest[..fits]);
                rest = rest[fits..];
                width = MeasureText(rest, fontSize);
            }

            line.Append(rest);
            lineWidth = width;
        }

        lines.Add(line.ToString());
    }

    /// <summary>
    /// The length of the longest prefix of <paramref name="word"/> that ends on a character
    /// boundary and fits <paramref name="maxWidth"/> — at least one character, so a character
    /// wider than the line still advances.
    /// </summary>
    int FittingPrefix(string word, float fontSize, float maxWidth)
    {
        var starts = StringInfo.ParseCombiningCharacters(word);

        int End(int count)
        {
            if (count < starts.Length)
            {
                return starts[count];
            }

            return word.Length;
        }

        // Binary search over the number of characters: width grows with the prefix, so the
        // answer is the last count that fits.
        var low = 1;
        var high = starts.Length;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (MeasureText(word[..End(middle)], fontSize) <= maxWidth)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        return End(low);
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