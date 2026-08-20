namespace Krilla.Html.Fonts;

/// <summary>
/// A run of text shaped once, and measurable by sub-range afterwards.
/// </summary>
/// <remarks>
/// <para>
/// Shaping is not cheap and line breaking asks about width constantly, so the two are separated:
/// the whole run is shaped once when it is created, and every later question is answered by
/// summing advances that are already known. Shaping each candidate substring instead would make
/// laying out a paragraph quadratic in its length.
/// </para>
/// <para>
/// This replaces summing raw <c>hmtx</c> advances, which was wrong in a way that only showed up
/// in aggregate: it ignores kerning and every ligature, so a measured word came out slightly too
/// wide and lines broke a word early. It is also why the corpus no longer has to disable those
/// features to be comparable to a browser.
/// </para>
/// </remarks>
sealed class ShapedText
{
    readonly Glyph[] glyphs;

    /// <summary>
    /// For each UTF-16 position in the text, the index of the first glyph starting at or after it.
    /// </summary>
    /// <remarks>
    /// Precomputed so a sub-range lookup is a pair of array reads rather than a scan. Shaping
    /// returns glyphs in text order for a left-to-right run, which is what makes a single forward
    /// index sufficient.
    /// </remarks>
    readonly int[] glyphStarts;

    ShapedText(string text, FontFace face, float fontSize, Glyph[] glyphs, int[] glyphStarts)
    {
        Text = text;
        Face = face;
        FontSize = fontSize;
        this.glyphs = glyphs;
        this.glyphStarts = glyphStarts;
    }

    /// <summary>The text that was shaped.</summary>
    public string Text { get; }

    /// <summary>The face it was shaped with.</summary>
    public FontFace Face { get; }

    /// <summary>The size its widths are reported at.</summary>
    public float FontSize { get; }

    /// <summary>Shapes <paramref name="text"/> with <paramref name="face"/>.</summary>
    public static ShapedText Create(FontFace face, string text, float fontSize)
    {
        var glyphs = text.Length == 0 ? [] : face.Shape(text);
        var starts = new int[text.Length + 1];

        var glyph = 0;
        for (var position = 0; position <= text.Length; position++)
        {
            while (glyph < glyphs.Length && glyphs[glyph].TextStart < position)
            {
                glyph++;
            }

            starts[position] = glyph;
        }

        return new(text, face, fontSize, glyphs, starts);
    }

    /// <summary>
    /// The width of the UTF-16 range [<paramref name="start"/>, <paramref name="end"/>), in layout
    /// units.
    /// </summary>
    public float Width(int start, int end)
    {
        var total = 0f;

        for (var index = glyphStarts[start]; index < glyphStarts[end]; index++)
        {
            total += glyphs[index].XAdvance;
        }

        return total / Face.UnitsPerEm * FontSize;
    }

    /// <summary>
    /// The glyphs covering a UTF-16 range, rebased onto the substring they cover.
    /// </summary>
    /// <remarks>
    /// Rebased so each painted run is self-contained: krilla indexes the text it is given by the
    /// glyphs' own ranges, and handing it the whole paragraph with offsets pointing into the
    /// middle would make every run's text-to-glyph mapping depend on where it started.
    /// </remarks>
    public (IReadOnlyList<Glyph> Glyphs, string Text) Slice(int start, int end)
    {
        var first = glyphStarts[start];
        var last = glyphStarts[end];

        if (last <= first)
        {
            return ([], "");
        }

        var sliced = new Glyph[last - first];

        for (var index = 0; index < sliced.Length; index++)
        {
            var glyph = glyphs[first + index];
            var glyphStart = Math.Clamp(glyph.TextStart - start, 0, end - start);
            var glyphEnd = Math.Clamp(glyph.TextStart + glyph.TextLength - start, glyphStart, end - start);

            sliced[index] = glyph with
            {
                TextStart = glyphStart,
                TextLength = glyphEnd - glyphStart
            };
        }

        return (sliced, Text[start..end]);
    }
}
