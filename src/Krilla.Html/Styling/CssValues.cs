/// <summary>
/// Parsers for the CSS value syntax AngleSharp.Css hands back as strings.
/// </summary>
/// <remarks>
/// AngleSharp.Css resolves the cascade and normalises most values into a predictable serialized
/// form — colours as <c>rgba(r, g, b, a)</c>, lengths with an explicit unit — but it does not
/// resolve relative units, so the numbers still have to be interpreted here against a font and
/// root context.
/// </remarks>
static class CssValues
{
    /// <summary>CSS pixels per point. A point is 1/72 inch, a CSS pixel 1/96.</summary>
    public const float PixelsPerPoint = 96f / 72f;

    /// <summary>
    /// Parses a length or percentage.
    /// </summary>
    /// <param name="value">The serialized value.</param>
    /// <param name="fontSize">The element's own font, for <c>em</c>, <c>ex</c> and <c>ch</c>.</param>
    /// <param name="root">The root font size and viewport, for <c>rem</c> and the <c>v*</c> units.</param>
    /// <param name="fallback">Returned when the value is missing or unparseable.</param>
    public static CssLength ParseLength(string? value, CssFont fontSize, CssRoot root, CssLength fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        // Ahead of the unit scan, because a calc() ends in a parenthesis rather than in a unit and
        // would otherwise be read as a number with an unrecognised one.
        if (CssCalc.Looks(value))
        {
            return CssCalc.Parse(value, fontSize, root) ?? fallback;
        }

        var text = value.AsSpan().Trim();

        if (text.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            return CssLength.Auto;
        }

        if (text.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return CssLength.None;
        }

        if (text.EndsWith('%'))
        {
            if (TryParseNumber(text[..^1], out var percent))
            {
                return CssLength.Percentage(percent);
            }

            return fallback;
        }

        var split = UnitStart(text);
        if (!TryParseNumber(text[..split], out var amount))
        {
            return fallback;
        }

        var unit = text[split..].Trim();

        // No unit below is longer than four characters, so anything longer cannot match one and
        // takes the fallback either way. Which is what lets the case folding CSS asks for happen
        // in a fixed buffer rather than in a string nobody keeps.
        if (unit.Length > 4)
        {
            return fallback;
        }

        Span<char> lower = stackalloc char[4];
        unit.ToLowerInvariant(lower);

        return lower[..unit.Length] switch
        {
            "px" or "" => CssLength.Pixels(amount),
            "pt" => CssLength.Pixels(amount * PixelsPerPoint),
            "pc" => CssLength.Pixels(amount * 12 * PixelsPerPoint),
            "in" => CssLength.Pixels(amount * 96),
            "cm" => CssLength.Pixels(amount * 96 / 2.54f),
            "mm" => CssLength.Pixels(amount * 96 / 25.4f),
            "q" => CssLength.Pixels(amount * 96 / 101.6f),
            "em" => CssLength.Pixels(amount * fontSize.Size),
            "rem" => CssLength.Pixels(amount * root.FontSize),
            // The viewport units. In paged media the viewport is the page's content box, so these
            // are a proportion of the sheet — resolved here rather than carried as percentages,
            // because unlike a percentage they do NOT depend on the containing block and a box
            // sized `50vw` is half the page wherever it sits.
            "vw" => CssLength.Pixels(amount * root.ViewportWidth / 100),
            "vh" => CssLength.Pixels(amount * root.ViewportHeight / 100),
            "vmin" => CssLength.Pixels(amount * root.ViewportMin / 100),
            "vmax" => CssLength.Pixels(amount * root.ViewportMax / 100),
            // Both come off the face rather than off the size. `ex` is the x-height and `ch`
            // the advance of "0", neither of which is a fixed fraction of the em: this face is
            // 0.5283 and 0.5561. They were approximated at half an em for as long as the value
            // layer threaded a bare float, and `CssFont` exists to carry the answer.
            "ex" => CssLength.Pixels(amount * fontSize.ExHeight),
            "ch" => CssLength.Pixels(amount * fontSize.ZeroAdvance),
            _ => fallback
        };
    }

    /// <summary>
    /// Splits a comma-separated list of layers, keeping functions and strings whole.
    /// </summary>
    /// <remarks>
    /// A plain split on commas cuts <c>rgba(0, 0, 0, 0.5)</c> into four, which is exactly the value
    /// a shadow layer carries — so the depth has to be tracked. Shared with anything else that takes
    /// a layer list.
    /// </remarks>
    public static List<string> SplitLayers(string value) =>
        Split(value, commas: true);

    /// <summary>
    /// Splits one layer into its space-separated components, keeping functions whole.
    /// </summary>
    public static List<string> SplitArguments(string value) =>
        Split(value, commas: false);

    static List<string> Split(string value, bool commas)
    {
        var parts = new List<string>();
        var start = 0;
        var depth = 0;
        var quote = '\0';

        for (var index = 0; index <= value.Length; index++)
        {
            if (index < value.Length)
            {
                var character = value[index];

                if (quote != '\0')
                {
                    if (character == quote && value[index - 1] != '\\')
                    {
                        quote = '\0';
                    }

                    continue;
                }

                switch (character)
                {
                    case '"' or '\'':
                        quote = character;
                        continue;
                    case '(':
                        depth++;
                        continue;
                    case ')':
                        depth--;
                        continue;
                }

                if (depth > 0)
                {
                    continue;
                }

                if (commas ? character != ',' : !char.IsWhiteSpace(character))
                {
                    continue;
                }
            }

            if (index > start)
            {
                var part = value[start..index].Trim();

                if (part.Length > 0)
                {
                    parts.Add(part);
                }
            }

            start = index + 1;
        }

        return parts;
    }

    /// <summary>
    /// Parses a colour, returning null for <c>transparent</c> and for anything unrecognised.
    /// </summary>
    /// <remarks>
    /// Null means "paint nothing", which is why fully transparent is folded into it: a zero-alpha
    /// fill and no fill at all are indistinguishable on the page, and collapsing them here keeps
    /// every painting site from having to check alpha.
    /// </remarks>
    public static Color? ParseColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();

        if (text.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (text.StartsWith('#'))
        {
            return ParseHex(text[1..]);
        }

        if (text.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            return ParseRgb(text);
        }

        return null;
    }

    /// <summary>
    /// The alpha of a colour value, 0-1. Returns 1 for anything without explicit alpha.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="ParseColor"/> because krilla models opacity as a fill property
    /// rather than as a fourth colour component — <see cref="Krilla.Color"/> has no alpha.
    /// </remarks>
    /// <summary>
    /// An angle in any of CSS's four units, in degrees, or null when the value is not one.
    /// </summary>
    /// <remarks>
    /// Read here rather than in the two places that need it, because the suffixes overlap:
    /// <c>grad</c> ends with <c>rad</c>, so the order the units are tested in is load-bearing and
    /// worth having once.
    /// </remarks>
    public static float? ParseAngle(string value)
    {
        var text = value.Trim().ToLowerInvariant();

        var (suffix, scale) = text switch
        {
            _ when text.EndsWith("deg", StringComparison.Ordinal) => ("deg", 1f),
            _ when text.EndsWith("grad", StringComparison.Ordinal) => ("grad", 0.9f),
            _ when text.EndsWith("turn", StringComparison.Ordinal) => ("turn", 360f),
            _ when text.EndsWith("rad", StringComparison.Ordinal) => ("rad", 180f / MathF.PI),
            _ => ("", 0f)
        };

        if (suffix.Length == 0)
        {
            return null;
        }

        if (float.TryParse(
                text[..^suffix.Length],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return parsed * scale;
        }

        return null;
    }

    public static float ParseAlpha(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 1f;
        }

        var text = value.Trim();

        if (text.Equals("transparent", StringComparison.OrdinalIgnoreCase))
        {
            return 0f;
        }

        if (text.StartsWith('#'))
        {
            var hex = text[1..];
            return hex.Length switch
            {
                4 => ParseHexDigit(hex[3]) * 17 / 255f,
                8 => int.Parse(hex.AsSpan(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255f,
                _ => 1f
            };
        }

        if (!text.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            return 1f;
        }

        var components = Components(text);
        if (components.Count >= 4 &&
            TryParseNumber(components[3], out var alpha))
        {
            return Math.Clamp(alpha, 0f, 1f);
        }

        return 1f;
    }

    static Color? ParseHex(string hex)
    {
        // A length that looks right is not enough: `#silver` is six characters and `byte.Parse`
        // throws on the first of them, where every other route out of `ParseColor` returns null.
        // Reachable from a stylesheet, and reached in practice by the legacy colour parse behind
        // `bgcolor`, which tries a bare value with a hash in front of it.
        foreach (var digit in hex)
        {
            if (!char.IsAsciiHexDigit(digit))
            {
                return null;
            }
        }

        // #rgb and #rgba expand by digit doubling; #rrggbb and #rrggbbaa are read directly.
        switch (hex.Length)
        {
            case 3:
            case 4:
                return Color.Rgb(
                    (byte) (ParseHexDigit(hex[0]) * 17),
                    (byte) (ParseHexDigit(hex[1]) * 17),
                    (byte) (ParseHexDigit(hex[2]) * 17));
            case 6:
            case 8:
                return Color.Rgb(
                    byte.Parse(hex.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(hex.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(hex.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            default:
                return null;
        }
    }

    static byte ParseHexDigit(char digit) =>
        (byte) (digit switch
        {
            >= '0' and <= '9' => digit - '0',
            >= 'a' and <= 'f' => digit - 'a' + 10,
            >= 'A' and <= 'F' => digit - 'A' + 10,
            _ => 0
        });

    static Color? ParseRgb(string text)
    {
        var components = Components(text);
        if (components.Count < 3)
        {
            return null;
        }

        if (!TryParseComponent(components[0], out var red) ||
            !TryParseComponent(components[1], out var green) ||
            !TryParseComponent(components[2], out var blue))
        {
            return null;
        }

        return Color.Rgb(red, green, blue);
    }

    /// <summary>
    /// The arguments inside a functional notation, split on commas and whitespace.
    /// </summary>
    /// <remarks>
    /// Both separators are accepted because both are legal: <c>rgb(0, 0, 0)</c> is the legacy
    /// syntax and <c>rgb(0 0 0 / 50%)</c> the modern one. The slash before alpha is treated as
    /// another separator, which is enough given the position of alpha is fixed.
    /// </remarks>
    static List<string> Components(string text)
    {
        var open = text.IndexOf('(');
        var close = text.LastIndexOf(')');
        if (open < 0 || close < open)
        {
            return [];
        }

        return [.. text[(open + 1)..close]
            .Split([',', ' ', '/', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
    }

    static bool TryParseComponent(CharSpan text, out byte value)
    {
        value = 0;

        if (text.EndsWith('%'))
        {
            if (!TryParseNumber(text[..^1], out var percent))
            {
                return false;
            }

            value = (byte) Math.Clamp(MathF.Round(percent * 255 / 100), 0, 255);
            return true;
        }

        if (!TryParseNumber(text, out var number))
        {
            return false;
        }

        value = (byte) Math.Clamp(MathF.Round(number), 0, 255);
        return true;
    }

    /// <summary>Where a dimension's unit begins, everything before it being the number.</summary>
    /// <remarks>
    /// An index rather than the two pieces, because a tuple cannot hold a
    /// <see cref="ReadOnlySpan{T}"/> - <c>ValueTuple</c>'s parameters do not allow a ref struct -
    /// and the caller can slice for itself.
    /// </remarks>
    static int UnitStart(CharSpan text)
    {
        var index = 0;
        while (index < text.Length &&
               (char.IsAsciiDigit(text[index]) || text[index] is '.' or '-' or '+' or 'e' or 'E'))
        {
            // An 'e' only belongs to the number in exponent position, where a digit or sign
            // follows it. Otherwise it starts the unit, as in "em".
            if (text[index] is 'e' or 'E' &&
                (index + 1 >= text.Length || !(char.IsAsciiDigit(text[index + 1]) || text[index + 1] is '-' or '+')))
            {
                break;
            }

            index++;
        }

        return index;
    }

    /// <summary>Parses a number in the invariant culture, as CSS always uses.</summary>
    public static bool TryParseNumber(CharSpan text, out float value) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    /// <summary>
    /// Splits a <c>font-family</c> list into its families, unquoting each.
    /// </summary>
    public static List<string> ParseFamilies(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var families = new List<string>();

        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var family = part;

            if (family.Length >= 2 &&
                ((family[0] == '"' && family[^1] == '"') ||
                 (family[0] == '\'' && family[^1] == '\'')))
            {
                family = family[1..^1];
            }

            if (family.Length > 0)
            {
                families.Add(family);
            }
        }

        return families;
    }
}
