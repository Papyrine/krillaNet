namespace Krilla.Html.Styling;

/// <summary>What one component of a <c>content</c> value contributes.</summary>
enum ContentKind
{
    /// <summary>A literal string.</summary>
    Text,

    /// <summary>The value of one of the host element's attributes.</summary>
    Attribute,

    /// <summary>One counter's value, in a counter style.</summary>
    Counter,

    /// <summary>Every value of one counter on the scope stack, joined by a separator.</summary>
    Counters,

    /// <summary>An image.</summary>
    Image,

    /// <summary>The opening or closing quotation mark for the current depth.</summary>
    Quote
}

/// <summary>
/// One component of a <c>content</c> value.
/// </summary>
/// <param name="Kind">Which of the six this is.</param>
/// <param name="Text">
/// The literal for <see cref="ContentKind.Text"/>, the attribute or counter name for the next
/// three, the URL for an image, and unused for a quote.
/// </param>
/// <param name="Separator">The separator for <see cref="ContentKind.Counters"/>.</param>
/// <param name="Style">The counter style for the two counter kinds.</param>
/// <param name="Opening">Whether a quote is an opening one.</param>
readonly record struct ContentItem(
    ContentKind Kind,
    string Text,
    string Separator = "",
    ListStyleKind Style = ListStyleKind.Decimal,
    bool Opening = true);

/// <summary>
/// Parses the <c>content</c> property of a <c>::before</c> or <c>::after</c>.
/// </summary>
/// <remarks>
/// <para>
/// AngleSharp hands the value back verbatim — strings quoted, <c>attr()</c>, <c>counter()</c>,
/// <c>counters()</c>, <c>url()</c> and the quote keywords all intact, including a concatenation of
/// several — so the whole of the grammar is readable and none of it had to be worked around.
/// </para>
/// <para>
/// Tokenising is by hand rather than by splitting on whitespace, because a string literal may
/// contain spaces and commas and a function's arguments certainly do. The scan therefore tracks
/// quotes and parenthesis depth, which is the only state it needs.
/// </para>
/// </remarks>
static class CssContent
{
    /// <summary>
    /// Parses <paramref name="value"/>, returning null when it generates no box.
    /// </summary>
    /// <remarks>
    /// <c>normal</c> and <c>none</c> both generate nothing on a pseudo-element, and are the
    /// overwhelmingly common case: <c>normal</c> is the initial value, so every element in a
    /// document has a <c>::before</c> whose content is nothing at all. Returning null rather than
    /// an empty list is what lets the caller skip the pseudo entirely.
    /// </remarks>
    public static List<ContentItem>? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();

        if (text.Equals("normal", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var items = new List<ContentItem>();

        foreach (var token in Tokens(text))
        {
            if (Item(token) is not {} item)
            {
                // One component nobody can read makes the whole value unusable rather than
                // partly usable: a counter dropped out of `counter(step) ". "` leaves a bare full
                // stop, which reads as a defect rather than as a missing feature.
                return null;
            }

            items.Add(item);
        }

        if (items.Count == 0)
        {
            return null;
        }

        return items;
    }

    /// <summary>
    /// Splits a value into its space-separated components, respecting quotes and parentheses.
    /// </summary>
    static IEnumerable<string> Tokens(string text)
    {
        var start = 0;
        var depth = 0;
        var quote = '\0';

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];

            if (quote != '\0')
            {
                if (character == quote && text[index - 1] != '\\')
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

            if (depth == 0 && char.IsWhiteSpace(character))
            {
                if (index > start)
                {
                    yield return text[start..index];
                }

                start = index + 1;
            }
        }

        if (start < text.Length)
        {
            yield return text[start..];
        }
    }

    static ContentItem? Item(string token)
    {
        if (token.Length >= 2 &&
            ((token[0] == '"' && token[^1] == '"') || (token[0] == '\'' && token[^1] == '\'')))
        {
            return new(ContentKind.Text, Unescape(token[1..^1]));
        }

        if (token.Equals("open-quote", StringComparison.OrdinalIgnoreCase))
        {
            return new(ContentKind.Quote, "");
        }

        if (token.Equals("close-quote", StringComparison.OrdinalIgnoreCase))
        {
            return new(ContentKind.Quote, "", Opening: false);
        }

        // The two that suppress a quote without drawing one still change the DEPTH, which is why
        // they are not simply dropped: `no-close-quote` pops the nesting level that a later
        // `close-quote` would otherwise reuse.
        if (token.Equals("no-open-quote", StringComparison.OrdinalIgnoreCase))
        {
            return new(ContentKind.Quote, "\0");
        }

        if (token.Equals("no-close-quote", StringComparison.OrdinalIgnoreCase))
        {
            return new(ContentKind.Quote, "\0", Opening: false);
        }

        if (Function(token, "attr") is {} attribute)
        {
            return new(ContentKind.Attribute, attribute.Trim());
        }

        if (Function(token, "url") is {} url)
        {
            return new(ContentKind.Image, Unquote(url.Trim()));
        }

        if (Function(token, "counters") is {} several)
        {
            var parts = Arguments(several);

            return parts.Count >= 2
                ? new(
                    ContentKind.Counters,
                    parts[0],
                    Unquote(parts[1]),
                    parts.Count >= 3 ? Counter(parts[2]) : ListStyleKind.Decimal)
                : null;
        }

        if (Function(token, "counter") is {} single)
        {
            var parts = Arguments(single);

            return parts.Count >= 1
                ? new(
                    ContentKind.Counter,
                    parts[0],
                    Style: parts.Count >= 2 ? Counter(parts[1]) : ListStyleKind.Decimal)
                : null;
        }

        return null;
    }

    /// <summary>The inside of <c>name(...)</c>, or null when the token is not that function.</summary>
    static string? Function(string token, string name) =>
        token.Length > name.Length + 2 &&
        token.StartsWith($"{name}(", StringComparison.OrdinalIgnoreCase) &&
        token[^1] == ')'
            ? token[(name.Length + 1)..^1]
            : null;

    /// <summary>
    /// Splits a function's arguments, on either a comma or whitespace, keeping strings whole.
    /// </summary>
    /// <remarks>
    /// Both separators, because AngleSharp normalises one of them away and not consistently:
    /// <c>counters(section, ".")</c> comes back as <c>counters(section .)</c> with the comma gone,
    /// while <c>counter(chapter, upper-roman)</c> keeps it. Splitting on commas alone therefore read
    /// the whole of the first as one argument and silently dropped every nested counter in the
    /// document. Accepting both is what makes the reading independent of which the serialiser chose.
    /// </remarks>
    static List<string> Arguments(string inside)
    {
        var parts = new List<string>();
        var start = 0;
        var quote = '\0';

        for (var index = 0; index <= inside.Length; index++)
        {
            if (index < inside.Length)
            {
                var character = inside[index];

                if (quote != '\0')
                {
                    if (character == quote && inside[index - 1] != '\\')
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (character is '\"' or '\'')
                {
                    quote = character;
                    continue;
                }

                if (character != ',' && !char.IsWhiteSpace(character))
                {
                    continue;
                }
            }

            if (index > start)
            {
                parts.Add(inside[start..index].Trim());
            }

            start = index + 1;
        }

        return parts;
    }

    static string Unquote(string value)
    {
        if (value.Length >= 2 &&
            ((value[0] == '"' &&
              value[^1] == '"') ||
             (value[0] == '\'' &&
              value[^1] == '\'')))
        {
            return Unescape(value[1..^1]);
        }

        return value;
    }

    /// <summary>
    /// Resolves the escapes a CSS string may carry.
    /// </summary>
    /// <remarks>
    /// Only the two that matter for generated content: a hexadecimal escape, which is how a
    /// stylesheet writes a character it cannot type — <c>"\201C"</c> for a left double quote is the
    /// usual one — and a backslash before any other character, which stands for that character.
    /// </remarks>
    static string Unescape(string value)
    {
        if (!value.Contains('\\'))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '\\' || index + 1 == value.Length)
            {
                builder.Append(value[index]);
                continue;
            }

            index++;
            var digits = 0;

            while (digits < 6 &&
                   index + digits < value.Length &&
                   Uri.IsHexDigit(value[index + digits]))
            {
                digits++;
            }

            if (digits == 0)
            {
                builder.Append(value[index]);
                continue;
            }

            builder.Append(char.ConvertFromUtf32(
                int.Parse(value.AsSpan(index, digits), NumberStyles.HexNumber, CultureInfo.InvariantCulture)));

            index += digits - 1;

            // One space after an escape is the terminator rather than content, which is what lets
            // `"\201C x"` mean a quote, a space and an x rather than a quote and then "x".
            if (index + 1 < value.Length && value[index + 1] == ' ')
            {
                index++;
            }
        }

        return builder.ToString();
    }

    static ListStyleKind Counter(string style) =>
        style.Trim().ToLowerInvariant() switch
        {
            "none" => ListStyleKind.None,
            "disc" => ListStyleKind.Disc,
            "circle" => ListStyleKind.Circle,
            "square" => ListStyleKind.Square,
            "decimal-leading-zero" => ListStyleKind.DecimalLeadingZero,
            "lower-alpha" or "lower-latin" => ListStyleKind.LowerAlpha,
            "upper-alpha" or "upper-latin" => ListStyleKind.UpperAlpha,
            "lower-roman" => ListStyleKind.LowerRoman,
            "upper-roman" => ListStyleKind.UpperRoman,
            _ => ListStyleKind.Decimal
        };
}
