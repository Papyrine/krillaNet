namespace Krilla.Html.Fonts;

/// <summary>
/// Turns a string into positioned glyphs, and measures one without positioning it.
/// </summary>
/// <remarks>
/// <para>
/// A character maps to a glyph and a glyph carries its <c>hmtx</c> advance — that is the whole
/// model. No kerning, no ligatures, no reordering, no bidi. Real shaping would come from the
/// rustybuzz krilla already links, exposed through krilla-capi; until it is, the corpus disables
/// the features this cannot do (see <c>Inputs/reset.css</c>) so that a browser's advances and
/// these agree exactly rather than approximately.
/// </para>
/// <para>
/// The output feeds <see cref="Surface.DrawGlyphs"/> rather than <see cref="Surface.DrawText"/>.
/// That is deliberate: <c>DrawText</c> shapes internally, and its advances would not be the ones
/// the line was broken with, so a line would paint slightly wider or narrower than it was laid
/// out. Measuring and painting from one set of numbers removes the possibility.
/// </para>
/// </remarks>
static class TextMeasurer
{
    /// <summary>
    /// Shapes <paramref name="text"/> at <paramref name="fontSize"/>.
    /// </summary>
    /// <remarks>
    /// Advances stay in design units, which is what <see cref="Surface.DrawGlyphs"/> takes — it
    /// normalises against <see cref="Font.UnitsPerEm"/> itself. <see cref="ShapedRun.Width"/> is
    /// in layout units, because that is what a line box needs.
    /// </remarks>
    public static ShapedRun Shape(FontFace face, string text, float fontSize)
    {
        if (text.Length == 0)
        {
            return new([], 0);
        }

        var glyphs = new List<Glyph>(text.Length);
        var designWidth = 0f;
        var index = 0;

        while (index < text.Length)
        {
            var length = char.IsHighSurrogate(text[index]) &&
                         index + 1 < text.Length &&
                         char.IsLowSurrogate(text[index + 1])
                ? 2
                : 1;

            var codepoint = length == 2 ? char.ConvertToUtf32(text[index], text[index + 1]) : text[index];
            var glyphId = face.GlyphIndex(codepoint);
            var advance = face.GlyphAdvance(glyphId);

            glyphs.Add(new(glyphId, advance, index, length));
            designWidth += advance;
            index += length;
        }

        return new(glyphs, designWidth / face.UnitsPerEm * fontSize);
    }

    /// <summary>
    /// The width of <paramref name="text"/> at <paramref name="fontSize"/>, in layout units.
    /// </summary>
    /// <remarks>
    /// Allocation-free, unlike <see cref="Shape"/>. Line breaking measures far more candidate
    /// substrings than it ever paints, so the split is worth having.
    /// </remarks>
    public static float Measure(FontFace face, ReadOnlySpan<char> text, float fontSize)
    {
        var designWidth = 0f;
        var index = 0;

        while (index < text.Length)
        {
            var length = char.IsHighSurrogate(text[index]) &&
                         index + 1 < text.Length &&
                         char.IsLowSurrogate(text[index + 1])
                ? 2
                : 1;

            var codepoint = length == 2 ? char.ConvertToUtf32(text[index], text[index + 1]) : text[index];
            designWidth += face.GlyphAdvance(face.GlyphIndex(codepoint));
            index += length;
        }

        return designWidth / face.UnitsPerEm * fontSize;
    }
}

/// <summary>
/// A shaped run: the glyphs to draw and the width they occupy.
/// </summary>
/// <param name="Glyphs">
/// Positioned glyphs, with advances in the font's design units.
/// </param>
/// <param name="Width">The run's width in layout units.</param>
readonly record struct ShapedRun(IReadOnlyList<Glyph> Glyphs, float Width);
