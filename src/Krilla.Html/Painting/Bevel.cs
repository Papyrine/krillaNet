/// <summary>
/// The two derived shades <c>inset</c>, <c>outset</c>, <c>groove</c> and <c>ridge</c> are drawn in.
/// </summary>
/// <remarks>
/// <para>
/// CSS specifies none of this. It says the four styles "look as though" the box were carved into or
/// coming out of the canvas and leaves every colour to the user agent, so — as with
/// <c>line-height: normal</c> and every number in <see cref="ListMarkers"/> — there is no correct
/// value to compute and agreeing with the reference browser is the only useful target.
/// </para>
/// <para>
/// The formulas below are Chromium's, recovered by measuring rendered borders at three colours and
/// then checking the derivation reproduces all three exactly. Both scale the colour by a factor
/// taken from its BRIGHTEST channel, which is what keeps a hue while moving its lightness — and
/// both truncate through a scale of 255.99998 rather than rounding, which is where the odd
/// off-by-one comes from: <c>#3366cc</c> lightens to <c>#3f7fff</c>, whose red is 63 where rounding
/// 63.75 would give 64.
/// </para>
/// <para>
/// The two hardcoded cases are Chromium's too, and they are not what the formula would give. White
/// has no brightest channel to scale down — the multiplier is 0.67 and would leave it at 171 by
/// accident rather than by rule — and black has none to scale up at all, where the formula divides
/// by zero. Both are named constants in Blink for the same reason they are named here.
/// </para>
/// </remarks>
static class Bevel
{
    /// <summary>
    /// Blink's scale, which is the largest float below 256.
    /// </summary>
    /// <remarks>
    /// Multiplying a 0..1 channel by this and truncating is what makes 1.0 come back as 255 rather
    /// than overflowing to 256, and it is also why every other channel lands a fraction low.
    /// </remarks>
    const float Scale = 255.99998f;

    /// <summary>What <c>inset</c> gives its top and left edges.</summary>
    public static Color Darken(Color color)
    {
        if (!color.TryGetRgb(out var red, out var green, out var blue))
        {
            return color;
        }

        // Chromium hardcodes white, and the value is not the formula's.
        if (red == 255 && green == 255 && blue == 255)
        {
            return Color.Rgb(0xAB, 0xAB, 0xAB);
        }

        var (r, g, b) = (red / 255f, green / 255f, blue / 255f);
        var value = MathF.Max(r, MathF.Max(g, b));

        // Black scales to black: the multiplier goes negative through the clamp, rather than the
        // division by zero it looks like.
        var multiplier = value == 0 ? 0 : MathF.Max(0, (value - 0.33f) / value);

        return Apply(multiplier, r, g, b);
    }

    /// <summary>What <c>outset</c> gives its top and left edges.</summary>
    public static Color Lighten(Color color)
    {
        if (!color.TryGetRgb(out var red, out var green, out var blue))
        {
            return color;
        }

        var (r, g, b) = (red / 255f, green / 255f, blue / 255f);
        var value = MathF.Max(r, MathF.Max(g, b));

        // Black, which has no channel to scale. Chromium's own constant.
        if (value == 0)
        {
            return Color.Rgb(0x54, 0x54, 0x54);
        }

        return Apply(MathF.Min(1, value + 0.33f) / value, r, g, b);
    }

    /// <summary>
    /// The shade one band of one edge is drawn in.
    /// </summary>
    /// <param name="color">The used border colour.</param>
    /// <param name="style">Which of the four bevelled styles, or another style to pass through.</param>
    /// <param name="near">Whether this is the top or left edge, rather than the bottom or right.</param>
    /// <param name="outer">Whether this is the outer half, which only the two-band styles read.</param>
    /// <param name="current">
    /// Whether <paramref name="color"/> came from <c>currentColor</c> rather than a declaration.
    /// </param>
    /// <remarks>
    /// <para>
    /// <c>groove</c> is the outer half drawn as <c>inset</c> and the inner half as <c>outset</c>;
    /// <c>ridge</c> is those two exchanged. That is Chromium's construction rather than a reading
    /// of the specification, and it is what the measured pixels show: a grooved top edge is dark
    /// over light where an inset one is dark throughout.
    /// </para>
    /// <para>
    /// An UNDECLARED colour takes <see cref="CurrentDark"/> and <see cref="CurrentLight"/> instead
    /// of anything derived. That is measured too, and it is the one rule here that a reading of
    /// either the specification or the formulas would never produce.
    /// </para>
    /// </remarks>
    public static Color Shade(Color color, BorderStyleKind style, bool near, bool outer, bool current) =>
        style switch
        {
            BorderStyleKind.Inset => Sunken(color, near, current),
            BorderStyleKind.Outset => Sunken(color, !near, current),
            BorderStyleKind.Groove => Sunken(color, outer == near, current),
            BorderStyleKind.Ridge => Sunken(color, outer != near, current),
            _ => color
        };

    /// <summary>
    /// The dark shade of an undeclared border colour, which is a constant rather than a derivation.
    /// </summary>
    /// <remarks>
    /// Measured. Four arrangements produce it — a box with <c>color: gray</c>, one with
    /// <c>color: black</c>, one declaring <c>border-color: currentColor</c>, and a plain
    /// <c>&lt;hr&gt;</c>, whose default stylesheet writes <c>border: 1px inset</c> and so leaves the
    /// colour at its initial value. All four give the same two shades, so Chromium is ignoring the
    /// element's own colour entirely here; a box that DECLARES <c>border-color: gray</c> gets the
    /// derivation instead, at 44 and 212.
    /// </remarks>
    static Color CurrentDark => Color.Rgb(0x9A, 0x9A, 0x9A);

    /// <inheritdoc cref="CurrentDark"/>
    static Color CurrentLight => Color.Rgb(0xEE, 0xEE, 0xEE);

    /// <summary>The shade an <c>inset</c> edge takes, which is dark at the top and left.</summary>
    static Color Sunken(Color color, bool dark, bool current)
    {
        if (current)
        {
            return dark ? CurrentDark : CurrentLight;
        }

        return dark ? Darken(color) : Lighten(color);
    }

    static Color Apply(float multiplier, float r, float g, float b) =>
        Color.Rgb(
            (byte) (multiplier * r * Scale),
            (byte) (multiplier * g * Scale),
            (byte) (multiplier * b * Scale));
}
