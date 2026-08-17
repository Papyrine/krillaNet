namespace Krilla.Html.Styling;

/// <summary>
/// The defaults a browser applies that AngleSharp.Css does not.
/// </summary>
/// <remarks>
/// <para>
/// AngleSharp.Css ships a default stylesheet, and it is the HTML 4.01 one rather than the modern
/// rendering rules a browser implements. Measured against Chrome, ten of twelve common elements
/// disagreed: every heading below <c>h1</c> has the wrong margin, <c>h4</c> and <c>p</c> have no
/// font size at all, and lists have no <c>padding-left</c> — so an unstyled document renders with
/// no list indentation whatsoever. <see cref="Corrections"/> fixes those.
/// </para>
/// <para>
/// It also has no <c>display</c> for the inline elements, reporting an empty string for
/// <c>&lt;b&gt;</c>, <c>&lt;i&gt;</c> and <c>&lt;span&gt;</c>. A box builder that reads an empty
/// display and assumes <c>block</c> puts every piece of emphasised text on a line of its own, so
/// those defaults are supplied in code rather than as CSS — the box builder consults them directly
/// when the cascade says nothing.
/// </para>
/// </remarks>
static class UserAgentStyles
{
    /// <summary>
    /// Corrections appended to AngleSharp's default stylesheet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Appended rather than replacing it, and appended into the SAME user-agent origin. Both
    /// matter. Appending keeps AngleSharp's rules for everything not restated here — the
    /// <c>display</c> table, <c>head { display: none }</c>, the table roles — and a later rule of
    /// equal specificity wins, which is what makes these override rather than merely coexist.
    /// Staying in the user-agent origin is what keeps an author stylesheet winning over them, as
    /// it must.
    /// </para>
    /// <para>
    /// Values are from the HTML Standard's rendering section, which is what browsers implement.
    /// Only the properties this engine honours are listed; there is no value in encoding
    /// <c>list-style-type</c> while list markers are not drawn.
    /// </para>
    /// </remarks>
    public const string Corrections =
        """
        h1 { font-size: 2em; margin: 0.67em 0; font-weight: bold; }
        h2 { font-size: 1.5em; margin: 0.83em 0; font-weight: bold; }
        h3 { font-size: 1.17em; margin: 1em 0; font-weight: bold; }
        h4 { font-size: 1em; margin: 1.33em 0; font-weight: bold; }
        h5 { font-size: 0.83em; margin: 1.67em 0; font-weight: bold; }
        h6 { font-size: 0.67em; margin: 2.33em 0; font-weight: bold; }

        p { margin: 1em 0; }
        blockquote { margin: 1em 40px; }
        figure { margin: 1em 40px; }
        pre { margin: 1em 0; font-family: monospace; }
        hr { margin: 0.5em auto; }

        ul, ol, menu { margin: 1em 0; padding-left: 40px; }
        dl { margin: 1em 0; }
        dd { margin-left: 40px; }

        /*
          A nested list drops its vertical margins entirely, which is why a multi-level outline
          reads as one block rather than gaining a blank line at every level.
        */
        ul ul, ul ol, ul menu, ol ul, ol ol, ol menu, menu ul, menu ol, menu menu { margin: 0; }

        /*
          Only an anchor WITH an href is underlined. A bare <a> is a target rather than a link,
          and browsers leave it undecorated.
        */
        a[href] { text-decoration: underline; }

        code, kbd, samp { font-family: monospace; }
        th { font-weight: bold; text-align: center; }
        center { text-align: center; }
        """;
    /// <summary>
    /// Elements that are inline-level unless a stylesheet says otherwise.
    /// </summary>
    /// <remarks>
    /// The HTML standard's rendering section. <c>img</c> belongs here despite being replaced
    /// rather than textual: it is inline-level, and treating it as a block puts it on a line of its
    /// own — so a picture in the middle of a sentence jumps below the paragraph text instead of
    /// flowing with it. The replaced elements still missing (<c>input</c>, <c>svg</c>, <c>video</c>)
    /// are absent because they need more than a display value to lay out.
    /// </remarks>
    static readonly HashSet<string> inline = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "abbr", "b", "bdi", "bdo", "br", "cite", "code", "data", "dfn", "em", "i", "img",
        "kbd", "label", "mark", "q", "rp", "rt", "ruby", "s", "samp", "small", "span", "strong",
        "sub", "sup", "time", "u", "var", "wbr"
    };

    /// <summary>
    /// Elements that generate no box, whatever their content.
    /// </summary>
    /// <remarks>
    /// A safety net rather than the primary mechanism — AngleSharp's default sheet already hides
    /// most of these. Without it a stylesheet's own text would be laid out as content, which is
    /// both wrong and extremely obvious.
    /// </remarks>
    static readonly HashSet<string> hidden = new(StringComparer.OrdinalIgnoreCase)
    {
        "head", "link", "meta", "script", "style", "title", "base", "template"
    };

    /// <summary>
    /// The display for <paramref name="localName"/>, or null when there is no default to apply and
    /// the cascade's answer should stand.
    /// </summary>
    public static DisplayKind? Display(string localName)
    {
        if (hidden.Contains(localName))
        {
            return DisplayKind.None;
        }

        return inline.Contains(localName) ? DisplayKind.Inline : null;
    }

    /// <summary>Whether <paramref name="localName"/> forces a line break.</summary>
    public static bool IsLineBreak(string localName) =>
        localName.Equals("br", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="localName"/> is bold by default.</summary>
    public static bool IsBold(string localName) =>
        localName is "b" or "strong" or "th";

    /// <summary>Whether <paramref name="localName"/> is italic by default.</summary>
    public static bool IsItalic(string localName) =>
        localName is "i" or "em" or "cite" or "var" or "dfn" or "address";
}
