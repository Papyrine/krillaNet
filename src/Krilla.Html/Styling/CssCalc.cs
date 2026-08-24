namespace Krilla.Html.Styling;

/// <summary>
/// Evaluates a <c>calc()</c> expression down to an absolute length plus a percentage.
/// </summary>
/// <remarks>
/// <para>
/// AngleSharp.Css hands <c>calc()</c> back verbatim, exactly as it does a bare percentage, so
/// evaluating it falls to this engine. Before that it fell through to the "unparseable" fallback
/// and the property silently took its default — which for a <c>width</c> means the box fills its
/// container, a difference no diagnostic reported because the value was never recognised as one
/// the engine could not honour.
/// </para>
/// <para>
/// What comes out is a sum of two components rather than a number, because the percentage half
/// cannot be resolved until a containing block exists. That is the same reason
/// <see cref="CssLength"/> carries a percentage at all; a <c>calc()</c> only adds the case where
/// both halves are present at once.
/// </para>
/// <para>
/// Whitespace around <c>+</c> and <c>-</c> is REQUIRED, which is CSS's own rule rather than a
/// simplification here. Without it <c>calc(2e-5px)</c> and <c>calc(10px -5px)</c> cannot be told
/// apart, and the sign of an exponent is not a subtraction. Honouring the rule is what lets the
/// tokeniser split on those operators at all.
/// </para>
/// </remarks>
static class CssCalc
{
    /// <summary>
    /// One operand: an absolute length in pixels, a percentage, or a dimensionless number.
    /// </summary>
    /// <remarks>
    /// A dimensionless number keeps its value in <see cref="Pixels"/> and is distinguished by the
    /// flag rather than by a separate field, because the only thing that treats the two
    /// differently is multiplication — which is exactly where CSS requires one side to be
    /// dimensionless.
    /// </remarks>
    readonly record struct Term(float Pixels, float Percent, bool Dimensionless)
    {
        public static Term Number(float value) => new(value, 0, true);

        public static Term Length(float pixels) => new(pixels, 0, false);

        public static Term Proportion(float percent) => new(0, percent, false);
    }

    /// <summary>
    /// Parses <paramref name="value"/> when it is a <c>calc()</c>, returning null when it is not
    /// one or cannot be evaluated.
    /// </summary>
    /// <remarks>
    /// Null covers both "not a calc" and "a calc this cannot do", which the caller treats alike:
    /// either way the value falls through to whatever it would have without this. The two are
    /// distinguished for reporting by <see cref="Looks"/>.
    /// </remarks>
    public static CssLength? Parse(string value, float fontSize, CssRoot root)
    {
        if (!Looks(value))
        {
            return null;
        }

        var tokens = Tokenize(value.Trim());
        if (tokens is null)
        {
            return null;
        }

        var index = 0;
        var result = Expression(tokens, ref index, fontSize, root);

        if (result is not {} term ||
            index != tokens.Count ||
            term.Dimensionless)
        {
            return null;
        }

        return CssLength.Sum(term.Pixels, term.Percent);
    }

    /// <summary>
    /// Whether the value is a <c>calc()</c> at all, however well-formed.
    /// </summary>
    /// <remarks>
    /// Read by <see cref="UnsupportedCss"/> as well as here, so a <c>calc()</c> carrying something
    /// this cannot evaluate — a <c>min()</c>, a unit outside the length set — is reported rather
    /// than silently taking the property's default.
    /// </remarks>
    public static bool Looks(string? value) =>
        value is not null &&
        value.AsSpan().TrimStart().StartsWith("calc(", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Splits an expression into operands, operators and parentheses, or null when a character
    /// belongs to none of them.
    /// </summary>
    /// <remarks>
    /// The leading <c>calc(</c> is dropped along with its closing parenthesis, and a nested
    /// <c>calc(</c> is dropped down to a bare <c>(</c> — the two mean the same thing inside an
    /// expression, so the grammar below needs only one of them.
    /// </remarks>
    static List<string>? Tokenize(string text)
    {
        var tokens = new List<string>();
        var index = 0;
        var depth = 0;

        while (index < text.Length)
        {
            var character = text[index];

            if (char.IsWhiteSpace(character))
            {
                index++;
                continue;
            }

            if (character is '(')
            {
                tokens.Add("(");
                depth++;
                index++;
                continue;
            }

            if (character is ')')
            {
                depth--;

                // The outermost close belongs to `calc(` itself and is not part of the grammar.
                if (depth == 0 && index == text.Length - 1)
                {
                    if (tokens.Count > 0 && tokens[0] == "(")
                    {
                        return tokens[1..];
                    }

                    return null;
                }

                tokens.Add(")");
                index++;
                continue;
            }

            if (character is '*' or '/')
            {
                tokens.Add(character.ToString());
                index++;
                continue;
            }

            // Only a plus or minus with space on both sides is an operator. Anything else is the
            // sign of the number that follows, which the operand scan below picks up.
            if (character is '+' or '-' &&
                index > 0 &&
                char.IsWhiteSpace(text[index - 1]) &&
                index + 1 < text.Length &&
                char.IsWhiteSpace(text[index + 1]))
            {
                tokens.Add(character.ToString());
                index++;
                continue;
            }

            if (text.AsSpan(index).StartsWith("calc(", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("(");
                depth++;
                index += 5;
                continue;
            }

            var start = index;

            while (index < text.Length &&
                   !char.IsWhiteSpace(text[index]) &&
                   text[index] is not ('(' or ')' or '*' or '/'))
            {
                index++;
            }

            if (index == start)
            {
                return null;
            }

            tokens.Add(text[start..index]);
        }

        return null;
    }

    /// <summary>Additive terms, left to right.</summary>
    static Term? Expression(List<string> tokens, ref int index, float fontSize, CssRoot root)
    {
        if (Product(tokens, ref index, fontSize, root) is not {} left)
        {
            return null;
        }

        while (index < tokens.Count && tokens[index] is "+" or "-")
        {
            var subtract = tokens[index] == "-";
            index++;

            if (Product(tokens, ref index, fontSize, root) is not {} right ||
                left.Dimensionless != right.Dimensionless)
            {
                return null;
            }

            var sign = subtract ? -1 : 1;
            left = new(
                left.Pixels + sign * right.Pixels,
                left.Percent + sign * right.Percent,
                left.Dimensionless);
        }

        return left;
    }

    /// <summary>Multiplicative terms, where one side of each has to be dimensionless.</summary>
    static Term? Product(List<string> tokens, ref int index, float fontSize, CssRoot root)
    {
        if (Operand(tokens, ref index, fontSize, root) is not {} left)
        {
            return null;
        }

        while (index < tokens.Count && tokens[index] is "*" or "/")
        {
            var divide = tokens[index] == "/";
            index++;

            if (Operand(tokens, ref index, fontSize, root) is not {} right)
            {
                return null;
            }

            if (divide)
            {
                if (!right.Dimensionless || right.Pixels == 0)
                {
                    return null;
                }

                left = new(left.Pixels / right.Pixels, left.Percent / right.Pixels, left.Dimensionless);
                continue;
            }

            if (right.Dimensionless)
            {
                left = new(left.Pixels * right.Pixels, left.Percent * right.Pixels, left.Dimensionless);
                continue;
            }

            if (!left.Dimensionless)
            {
                // Two lengths multiplied is an area, which no property takes.
                return null;
            }

            left = new(right.Pixels * left.Pixels, right.Percent * left.Pixels, false);
        }

        return left;
    }

    /// <summary>A parenthesised expression, or a single number, length or percentage.</summary>
    static Term? Operand(List<string> tokens, ref int index, float fontSize, CssRoot root)
    {
        if (index >= tokens.Count)
        {
            return null;
        }

        if (tokens[index] == "(")
        {
            index++;
            var inner = Expression(tokens, ref index, fontSize, root);

            if (inner is null ||
                index >= tokens.Count ||
                tokens[index] != ")")
            {
                return null;
            }

            index++;
            return inner;
        }

        var token = tokens[index];

        if (token is "+" or "-" or "*" or "/" or ")")
        {
            return null;
        }

        index++;
        return Value(token, fontSize, root);
    }

    /// <summary>
    /// One literal: a dimensionless number, a percentage, or any length unit
    /// <see cref="CssValues.ParseLength"/> knows.
    /// </summary>
    /// <remarks>
    /// Routed through the same parser as a standalone length rather than repeating the unit table,
    /// so a unit added there works inside <c>calc()</c> without a second edit. The fallback is
    /// <see cref="CssLength.None"/> because it is the one kind no real length produces, which makes
    /// "unparseable" distinguishable from a genuine zero.
    /// </remarks>
    static Term? Value(string token, float fontSize, CssRoot root)
    {
        if (CssValues.TryParseNumber(token, out var number))
        {
            return Term.Number(number);
        }

        var length = CssValues.ParseLength(token, fontSize, root, CssLength.None);

        return length.Kind switch
        {
            LengthKind.Absolute => Term.Length(length.Value),
            LengthKind.Percent => Term.Proportion(length.Value),
            _ => null
        };
    }
}
