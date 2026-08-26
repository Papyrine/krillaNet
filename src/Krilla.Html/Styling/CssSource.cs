namespace Krilla.Html.Styling;

/// <summary>
/// Reads declarations out of a stylesheet's own source, for the properties the parser drops.
/// </summary>
/// <remarks>
/// <para>
/// The second place this repository reads CSS as text rather than through the object model, and
/// for the same reason as the first: AngleSharp.Css parses a declaration it does not recognise
/// into nothing at all, so the value is gone from the rule, gone from its <c>CssText</c>, and
/// indistinguishable from a declaration nobody wrote. <see cref="PageRules"/> recovers
/// <c>@page</c>'s <c>size</c>, its selector and its margin boxes that way; this recovers the two
/// CSS Paged Media properties that live on ordinary ELEMENTS, <c>string-set</c> and <c>page</c>,
/// which no at-rule scan can reach.
/// </para>
/// <para>
/// Bounded the way that one is. It looks for one named property, finds the block enclosing each
/// occurrence by matching braces BACKWARDS, and hands back that block's prelude with the value.
/// Nesting therefore costs nothing — a rule inside <c>@media print</c> yields its own selector
/// rather than the at-rule's — and an at-rule's own declarations are skipped by the prelude
/// starting with <c>@</c>.
/// </para>
/// <para>
/// It reads no comments and no strings, so a property name inside either would be found. That is
/// the same exposure the <c>@page</c> scan carries, and the same answer applies: the alternative is
/// a second CSS parser.
/// </para>
/// </remarks>
static class CssSource
{
    /// <summary>
    /// Every declaration of <paramref name="property"/> in <paramref name="text"/>, with the
    /// selector list of the rule carrying it, in source order.
    /// </summary>
    public static IEnumerable<(string Selectors, string Value)> Declarations(string text, string property)
    {
        var index = 0;

        while (true)
        {
            index = text.IndexOf(property, index, StringComparison.OrdinalIgnoreCase);

            if (index < 0)
            {
                yield break;
            }

            var after = index + property.Length;

            if (Value(text, index, after) is {} value &&
                Enclosing(text, index) is {} open &&
                Prelude(text, open) is {Length: > 0} selectors &&
                selectors[0] != '@')
            {
                yield return (selectors, value);
            }

            index = after;
        }
    }

    /// <summary>
    /// One property's value inside a declaration BLOCK, without the surrounding rule.
    /// </summary>
    /// <remarks>
    /// For a value the parser rejected outright. A margin box's <c>content</c> is the case: a
    /// <c>string()</c> anywhere in it makes AngleSharp drop the whole declaration, so the running
    /// header the property was written for disappears along with the literals around it.
    /// </remarks>
    public static string? Declaration(string body, string property)
    {
        var index = 0;

        while (true)
        {
            index = body.IndexOf(property, index, StringComparison.OrdinalIgnoreCase);

            if (index < 0)
            {
                return null;
            }

            var after = index + property.Length;

            if (Value(body, index, after) is {} value)
            {
                return value;
            }

            index = after;
        }
    }

    /// <summary>
    /// The value of the declaration starting at <paramref name="index"/>, or null when what was
    /// found is not a property name at all.
    /// </summary>
    /// <remarks>
    /// The name has to be whole — <c>page</c> must not match the tail of <c>break-before-page</c>
    /// or the head of <c>page-break-before</c> — and has to be followed by its colon with nothing
    /// but space between.
    /// </remarks>
    static string? Value(string text, int index, int after)
    {
        if (index > 0 && (char.IsLetterOrDigit(text[index - 1]) || text[index - 1] == '-'))
        {
            return null;
        }

        var colon = text.IndexOf(':', after);

        if (colon < 0 || text[after..colon].Trim().Length > 0)
        {
            return null;
        }

        var end = text.IndexOfAny([';', '}'], colon);
        var declared = (end < 0 ? text[(colon + 1)..] : text[(colon + 1)..end]).Trim();

        return declared.Length == 0 ? null : declared;
    }

    /// <summary>
    /// The opening brace of the block containing <paramref name="index"/>, or null when it is not
    /// inside one.
    /// </summary>
    /// <remarks>
    /// Scanned BACKWARDS with a depth counter, which is what makes nesting free: a closing brace
    /// passed on the way up is a sibling block, and the first opening brace that is not matched by
    /// one is this declaration's own.
    /// </remarks>
    static int? Enclosing(string text, int index)
    {
        var depth = 0;

        for (var scan = index - 1; scan >= 0; scan--)
        {
            if (text[scan] == '}')
            {
                depth++;
                continue;
            }

            if (text[scan] != '{')
            {
                continue;
            }

            if (depth == 0)
            {
                return scan;
            }

            depth--;
        }

        return null;
    }

    /// <summary>What stands before a block's opening brace: its selector list, or its at-rule.</summary>
    static string Prelude(string text, int open)
    {
        var start = 0;

        for (var scan = open - 1; scan >= 0; scan--)
        {
            if (text[scan] is '{' or '}' or ';')
            {
                start = scan + 1;
                break;
            }
        }

        return text[start..open].Trim();
    }
}
