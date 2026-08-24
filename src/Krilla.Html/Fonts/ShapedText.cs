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

    readonly float letterSpacing;
    readonly float wordSpacing;

    ShapedText(
        string text,
        FontFace face,
        float fontSize,
        float letterSpacing,
        float wordSpacing,
        Glyph[] glyphs,
        int[] glyphStarts)
    {
        Text = text;
        Face = face;
        FontSize = fontSize;
        this.letterSpacing = letterSpacing;
        this.wordSpacing = wordSpacing;
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
    /// <param name="face">The face to shape with.</param>
    /// <param name="text">The text to shape.</param>
    /// <param name="fontSize">The size widths are reported at.</param>
    /// <param name="letterSpacing">Extra advance after each character, in CSS pixels.</param>
    /// <param name="wordSpacing">Extra advance added to each space, in CSS pixels.</param>
    /// <remarks>
    /// The two spacings are applied here rather than by the caller because they change the answer
    /// to every question this class exists to answer. A width that ignored them would break lines
    /// in the wrong places and size a shrink-wrapped box to the wrong width, and adding them
    /// afterwards at only some of the call sites is the shape that bug would take.
    /// </remarks>
    public static ShapedText Create(
        FontFace face,
        string text,
        float fontSize,
        float letterSpacing = 0,
        float wordSpacing = 0)
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

        return new(text, face, fontSize, letterSpacing, wordSpacing, glyphs, starts);
    }

    /// <summary>
    /// The extra advance the two spacing properties add over a UTF-16 range, in CSS pixels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Letter spacing is per CHARACTER rather than per glyph, and the difference is visible
    /// wherever a ligature covers several characters: <c>office</c> with 3px of spacing is 18px
    /// wider in a browser, not 12px, even though the <c>ffi</c> is drawn as one glyph. Measured,
    /// and it is what decided this — spacing per glyph is the reading a shaper invites.
    /// </para>
    /// <para>
    /// After each character INCLUDING the last, which is why a shrink-wrapped box carries the
    /// spacing past its final glyph. Seven characters at 3px are 21px wider, not 18px.
    /// </para>
    /// <para>
    /// Counted in UTF-16 code units, so a character outside the basic plane is spaced twice. No
    /// scenario reaches one, and the alternative is a rune walk on the hottest measurement path in
    /// line breaking.
    /// </para>
    /// </remarks>
    float Spacing(int start, int end)
    {
        if (letterSpacing == 0 && wordSpacing == 0)
        {
            return 0;
        }

        var total = letterSpacing * (end - start);

        if (wordSpacing != 0)
        {
            for (var index = start; index < end; index++)
            {
                if (Text[index] == ' ')
                {
                    total += wordSpacing;
                }
            }
        }

        return total;
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

        return total / Face.UnitsPerEm * FontSize + Spacing(start, end);
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

            // The spacing this glyph carries, converted from CSS pixels back into font units
            // because that is what an advance is. Attributed to the glyph covering the characters
            // it is owed to, which keeps the run's painted width equal to its measured one however
            // the shaper grouped the text.
            var spacing = Spacing(glyph.TextStart, glyph.TextStart + glyph.TextLength);

            sliced[index] = glyph with
            {
                TextStart = glyphStart,
                TextLength = glyphEnd - glyphStart,
                XAdvance = glyph.XAdvance + spacing / FontSize * Face.UnitsPerEm
            };
        }

        return (sliced, Text[start..end]);
    }
}
