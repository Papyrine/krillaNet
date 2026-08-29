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
/// Comments are stripped first, by <see cref="WithoutComments"/>, and that was not an optimisation
/// — a comment written above a rule became part of that rule's selector, so the scan worked on an
/// undocumented stylesheet and silently stopped working on a documented one. Strings are still not
/// read, so a property name inside one would be found, and a <c>/*</c> inside one still starts a
/// comment. That is the same exposure the <c>@page</c> scan carries, and the same answer applies:
/// the alternative is a second CSS parser.
/// </para>
/// </remarks>
static class CssSource
{
    /// <summary>
    /// Every declaration of <paramref name="property"/> in <paramref name="source"/>, with the
    /// selector list of the rule carrying it, in source order.
    /// </summary>
    public static IEnumerable<(string Selectors, string Value)> Declarations(string source, string property)
    {
        var text = WithoutComments(source);
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
    public static string? Declaration(string source, string property)
    {
        var body = WithoutComments(source);
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
    /// <paramref name="text"/> with every CSS comment replaced by a single space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every scan below counts braces, searches for a colon, and reads backwards to a rule's
    /// selector — and a comment can defeat all three. The one that actually bit is the third:
    /// <see cref="Prelude"/> reads back from a block's opening brace to the previous <c>;</c>,
    /// <c>{</c> or <c>}</c>, so a comment written ABOVE a rule becomes part of that rule's
    /// selector list, and the rule is then matched against nothing. Which is to say the scan
    /// worked on a stylesheet with no comments in it and silently stopped working on a documented
    /// one — and a stylesheet worth recovering a declaration from is exactly the kind that has
    /// comments.
    /// </para>
    /// <para>
    /// A SPACE rather than nothing, because a CSS comment separates tokens: <c>a/**/b</c> is two
    /// identifiers and removing the comment outright would join them into one.
    /// </para>
    /// <para>
    /// It reads no strings, so a <c>/*</c> inside a quoted value still starts a comment here. That
    /// is the same exposure the scan already carried, narrowed rather than removed — the
    /// alternative is a second CSS parser.
    /// </para>
    /// </remarks>
    public static string WithoutComments(string text)
    {
        var start = text.IndexOf("/*", StringComparison.Ordinal);

        if (start < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        var index = 0;

        while (start >= 0)
        {
            builder.Append(text, index, start - index);
            builder.Append(' ');

            var end = text.IndexOf("*/", start + 2, StringComparison.Ordinal);

            // An unterminated comment runs to the end of the stylesheet, which is what a CSS
            // parser does with one too.
            if (end < 0)
            {
                return builder.ToString();
            }

            index = end + 2;
            start = text.IndexOf("/*", index, StringComparison.Ordinal);
        }

        builder.Append(text, index, text.Length - index);

        return builder.ToString();
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
