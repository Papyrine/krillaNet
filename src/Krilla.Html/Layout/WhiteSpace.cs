namespace Krilla.Html.Layout;

/// <summary>
/// The white-space processing CSS applies to text before it is flowed into lines.
/// </summary>
/// <remarks>
/// This is why an HTML source file can be indented without the indentation appearing on the page.
/// The rules are from CSS Text 3 §4.1, in the phase order the specification gives: segment breaks
/// are transformed first, then tabs, then sequences of spaces are collapsed. Running them in a
/// different order gives different answers for text containing a newline surrounded by spaces,
/// which is what most indented markup is.
/// </remarks>
static class WhiteSpace
{
    /// <summary>
    /// Processes <paramref name="text"/> under <paramref name="style"/>'s white-space value.
    /// </summary>
    /// <remarks>
    /// Leading and trailing spaces survive here and are trimmed by
    /// <see cref="InlineLayout"/> at the point a line starts or ends. They have to: whether the
    /// space between <c>&lt;/b&gt;</c> and <c>&lt;i&gt;</c> is dropped depends on where the line
    /// breaks, which is not known yet.
    /// </remarks>
    public static string Process(string text, ComputedStyle style)
    {
        if (text.Length == 0)
        {
            return "";
        }

        if (style.WhiteSpace is WhiteSpaceKind.Pre or WhiteSpaceKind.PreWrap)
        {
            // Preserved verbatim, save for newline normalisation — a CRLF in the source is one
            // segment break, not two.
            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        var preserveNewlines = style.WhiteSpace == WhiteSpaceKind.PreLine;
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;

        foreach (var character in text)
        {
            if (character is '\r')
            {
                continue;
            }

            if (character is '\n')
            {
                if (preserveNewlines)
                {
                    // A forced break swallows the spaces around it, so drop anything pending
                    // rather than emitting a space before the break.
                    pendingSpace = false;
                    builder.Append('\n');
                    continue;
                }

                pendingSpace = true;
                continue;
            }

            if (character is ' ' or '\t' or '\f')
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace)
            {
                // A whole run of mixed white space collapses to exactly one space, whatever it
                // was made of.
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        if (pendingSpace)
        {
            builder.Append(' ');
        }

        return builder.ToString();
    }
}
