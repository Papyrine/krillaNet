namespace Krilla.Html.Styling;

/// <summary>
/// The font a relative length resolves against: the size, plus the two quantities that cannot be
/// derived from it.
/// </summary>
/// <remarks>
/// <para>
/// <c>em</c> and <c>rem</c> need a number. <c>ex</c> and <c>ch</c> need a FACE — the x-height and
/// the advance of <c>0</c> are properties of the glyphs, not of the size — so the whole of the
/// value layer threads this rather than a bare float. That is why the type exists at all: it was
/// a float everywhere, both units were approximated at half an em, and there was nowhere to put
/// the answer.
/// </para>
/// <para>
/// <see cref="Approximate"/> keeps that old behaviour for the parses that cannot see a face, and
/// it is deliberately not an implicit conversion. An implicit one would let a call site pass a
/// size where a font belongs and silently go back to approximating, which is exactly the failure
/// this replaced.
/// </para>
/// </remarks>
readonly record struct CssFont(float Size, float ExHeight, float ZeroAdvance)
{
    /// <summary>
    /// The conventional defaults, for a size with no face behind it.
    /// </summary>
    /// <remarks>
    /// Half an em for both. Wrong for every real face — Liberation Sans is 0.5283 of the em at the
    /// x-height and 0.5561 at the advance of <c>0</c> — and it is what a parse with no font
    /// resolved has to fall back to.
    /// </remarks>
    public static CssFont Approximate(float size) =>
        new(size, size / 2, size / 2);

    /// <summary>
    /// The font <paramref name="face"/> at <paramref name="size"/>, or the approximation when no
    /// face resolved.
    /// </summary>
    public static CssFont For(FontFace? face, float size)
    {
        if (face is null)
        {
            return Approximate(size);
        }

        var zero = face.Advance('0', size);

        return new(
            size,
            face.XHeight(size),
            // A face with no `0` glyph reports a zero advance, and a `ch` of zero collapses the box
            // rather than sizing it. The approximation is the better wrong answer.
            zero > 0 ? zero : size / 2);
    }
}
