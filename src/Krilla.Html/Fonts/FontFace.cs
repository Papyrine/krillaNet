namespace Krilla.Html.Fonts;

/// <summary>
/// One font file: the metrics that measure it, paired with the krilla handle that paints it.
/// </summary>
/// <remarks>
/// <para>
/// The pairing is the point. krilla owns the glyph outlines and the embedding but exposes no
/// metrics; <see cref="OpenTypeMetrics"/> reads the metrics but cannot draw. Layout needs both to
/// agree, and they do here because both read the same bytes.
/// </para>
/// <para>
/// The krilla handle is created on first paint, not on load. Measuring is therefore possible
/// without the native library present at all, which is what lets layout be exercised — and the
/// whole box comparison against a browser be run — independently of the PDF writer. The
/// dependency runs one way: painting needs measurement, measurement does not need painting.
/// </para>
/// </remarks>
public sealed class FontFace :
    IDisposable
{
    readonly OpenTypeMetrics metrics;
    readonly byte[] data;
    readonly uint index;
    Font? font;

    FontFace(byte[] data, uint index, OpenTypeMetrics metrics, string family, int weight, bool italic)
    {
        this.data = data;
        this.index = index;
        this.metrics = metrics;
        Family = family;
        Weight = weight;
        Italic = italic;
    }

    /// <summary>
    /// The krilla font, parsed on first use.
    /// </summary>
    /// <remarks>
    /// Parsing is comparatively expensive and a document rarely paints with every registered face,
    /// so deferring it also means an unused face in the set costs only the bytes it was read from.
    /// </remarks>
    internal Font Font => font ??= Font.Load(data, index);

    /// <summary>The family this face belongs to, as CSS would name it.</summary>
    public string Family { get; }

    /// <summary>The weight class, 100-900.</summary>
    public int Weight { get; }

    /// <summary>Whether this face is the italic or oblique one.</summary>
    public bool Italic { get; }

    /// <summary>Design units per em.</summary>
    public float UnitsPerEm => metrics.UnitsPerEm;

    /// <summary>
    /// Distance from the baseline to the top of the em box, in CSS pixels at
    /// <paramref name="fontSize"/>.
    /// </summary>
    /// <remarks>
    /// Rounded to a whole pixel. See <see cref="NormalLineHeight"/> for why.
    /// </remarks>
    public float Ascent(float fontSize) =>
        MathF.Round(metrics.Ascender * fontSize / metrics.UnitsPerEm);

    /// <summary>
    /// Distance from the baseline to the bottom of the em box, in CSS pixels at
    /// <paramref name="fontSize"/>. Positive, unlike the font's own signed value, because every
    /// caller wants a magnitude to add.
    /// </summary>
    public float Descent(float fontSize) =>
        MathF.Round(-metrics.Descender * fontSize / metrics.UnitsPerEm);

    /// <summary>
    /// What <c>line-height: normal</c> resolves to at <paramref name="fontSize"/>, in CSS pixels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ascent plus descent plus line gap, each rounded to a whole pixel BEFORE being summed. The
    /// rounding is the whole subtlety, and it is deliberate: at 16px Liberation Sans gives
    /// 14.48 + 3.39 + 0.52, which sums to 18.4 unrounded but to 14 + 3 + 1 = 18 the way a browser
    /// does it. Four tenths of a pixel per line is invisible on one line and a whole line of drift
    /// down a long page.
    /// </para>
    /// <para>
    /// This is the one place the engine imitates a specific implementation rather than following a
    /// specification, and it is because there is no specification to follow: CSS defines
    /// <c>normal</c> as "a reasonable value based on the font", explicitly leaving it to the user
    /// agent. With no correct answer to compute, agreeing with the browser the corpus is measured
    /// against is the useful choice.
    /// </para>
    /// </remarks>
    public float NormalLineHeight(float fontSize) =>
        Ascent(fontSize) +
        Descent(fontSize) +
        MathF.Round(metrics.LineGap * fontSize / metrics.UnitsPerEm);

    /// <summary>
    /// Loads a face from a font file, taking its family, weight and style from the file itself.
    /// </summary>
    /// <remarks>
    /// Reading them from the font rather than the filename means a face cannot be registered
    /// under a description that contradicts what it actually is.
    /// </remarks>
    public static FontFace LoadFile(string path, uint index = 0) =>
        Load(File.ReadAllBytes(path), index);

    /// <summary>Loads a face from memory.</summary>
    public static FontFace Load(ReadOnlySpan<byte> data, uint index = 0)
    {
        var copy = data.ToArray();
        var metrics = OpenTypeMetrics.Read(copy, index);
        return new(copy, index, metrics, metrics.FamilyName, metrics.Weight, metrics.Italic);
    }

    /// <summary>
    /// Distance from the baseline down to the top of an underline, in CSS pixels, rounded to a
    /// whole pixel as browsers do.
    /// </summary>
    public float UnderlineOffset(float fontSize) =>
        MathF.Round(metrics.UnderlineOffset * fontSize / metrics.UnitsPerEm);

    /// <summary>
    /// Underline thickness in CSS pixels, never less than one: a rule rounded away to nothing is
    /// worse than one a little too thick.
    /// </summary>
    public float UnderlineThickness(float fontSize) =>
        MathF.Max(1, MathF.Round(metrics.UnderlineThickness * fontSize / metrics.UnitsPerEm));

    /// <summary>
    /// The advance of <paramref name="codepoint"/> at <paramref name="fontSize"/>, in the same
    /// units as the size.
    /// </summary>
    public float Advance(int codepoint, float fontSize) =>
        metrics.Advance(metrics.GlyphIndex(codepoint)) / metrics.UnitsPerEm * fontSize;

    /// <summary>
    /// Shapes <paramref name="text"/>, applying kerning, ligatures and the font's own
    /// substitutions.
    /// </summary>
    /// <remarks>
    /// The one operation here that needs the native library, because the shaper lives in krilla.
    /// Everything else this type does is read out of the font bytes in managed code.
    /// </remarks>
    public Glyph[] Shape(string text) =>
        Font.Shape(text);

    /// <summary>The glyph for <paramref name="codepoint"/>, or 0 when the face lacks it.</summary>
    public ushort GlyphIndex(int codepoint) =>
        metrics.GlyphIndex(codepoint);

    /// <summary>The advance of <paramref name="glyphId"/>, in design units.</summary>
    public float GlyphAdvance(ushort glyphId) =>
        metrics.Advance(glyphId);

    /// <summary>Whether the face has a glyph for <paramref name="codepoint"/>.</summary>
    public bool Covers(int codepoint) =>
        metrics.GlyphIndex(codepoint) != 0;

    /// <inheritdoc />
    public void Dispose()
    {
        // Nothing to release when the face was only ever measured with.
        font?.Dispose();
        font = null;
    }
}
