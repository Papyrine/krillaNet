namespace Krilla;

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