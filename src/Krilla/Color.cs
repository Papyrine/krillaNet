namespace Krilla;

/// <summary>
/// A device colour in one of the three spaces krilla supports directly.
/// </summary>
/// <remarks>
/// Components are 8 bit throughout, matching krilla's own constructors. Spot (separation)
/// colours are not represented here: they carry a colorant name and a fallback colour.
/// </remarks>
public readonly record struct Color
{
    internal const int SpaceRgb = 0;
    internal const int SpaceLuma = 1;
    internal const int SpaceCmyk = 2;

    Color(int space, byte c0, byte c1, byte c2, byte c3)
    {
        Space = space;
        C0 = c0;
        C1 = c1;
        C2 = c2;
        C3 = c3;
    }

    internal int Space { get; }

    internal byte C0 { get; }

    internal byte C1 { get; }

    internal byte C2 { get; }

    internal byte C3 { get; }

    public static Color Rgb(byte red, byte green, byte blue) =>
        new(SpaceRgb, red, green, blue, 0);

    /// <summary>
    /// Creates a greyscale colour, where 0 is black and 255 is white.
    /// </summary>
    public static Color Gray(byte lightness) =>
        new(SpaceLuma, lightness, 0, 0, 0);

    /// <remarks>
    /// PDF/A requires an ICC profile for CMYK content; without one, a document using CMYK
    /// fails archival validation when it is finished.
    /// </remarks>
    public static Color Cmyk(byte cyan, byte magenta, byte yellow, byte black) =>
        new(SpaceCmyk, cyan, magenta, yellow, black);

    /// <summary>
    /// Black.
    /// </summary>
    public static Color Black => Gray(0);

    /// <summary>
    /// White.
    /// </summary>
    public static Color White => Gray(255);

    /// <summary>
    /// This colour's red, green and blue components, when it has any.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Grey converts exactly: one lightness stands for all three channels. CMYK does not, and is
    /// refused rather than approximated — the conversion back depends on the device and the
    /// profile, so any answer here would be a guess presented as a measurement.
    /// </para>
    /// <para>
    /// A colour could be constructed and never read back, which is the gap this closes. Anything
    /// deriving one colour from another needs the components: a bevelled CSS border is the first
    /// such caller, since <c>inset</c> and its three relatives are drawn in two shades the author
    /// never wrote.
    /// </para>
    /// </remarks>
    /// <returns>Whether the colour is in a space with RGB components.</returns>
    public bool TryGetRgb(out byte red, out byte green, out byte blue)
    {
        switch (Space)
        {
            case SpaceRgb:
                (red, green, blue) = (C0, C1, C2);
                return true;
            case SpaceLuma:
                (red, green, blue) = (C0, C0, C0);
                return true;
            default:
                (red, green, blue) = (0, 0, 0);
                return false;
        }
    }

    internal NativeColor ToNative() =>
        new()
        {
            Space = Space,
            C0 = C0,
            C1 = C1,
            C2 = C2,
            C3 = C3
        };
}

/// <summary>
/// A colour stop in a gradient.
/// </summary>
/// <param name="Offset">Position along the gradient, from 0 to 1.</param>
/// <param name="Color">The colour at this position.</param>
/// <param name="Opacity">Opacity at this position, from 0 to 1.</param>
public readonly record struct GradientStop(float Offset, Color Color, float Opacity = 1f);
