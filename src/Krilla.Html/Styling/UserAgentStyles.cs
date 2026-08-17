namespace Krilla.Html.Styling;

/// <summary>
/// The defaults a browser applies that AngleSharp.Css does not.
/// </summary>
/// <remarks>
/// <para>
/// AngleSharp.Css ships a default stylesheet, but it does not cover <c>display</c> for the inline
/// elements. It reports an empty string for <c>&lt;b&gt;</c>, <c>&lt;i&gt;</c>, <c>&lt;span&gt;</c>
/// and the rest, and a box builder that reads an empty display and assumes <c>block</c> puts every
/// piece of emphasised text on a line of its own. That is a whole-paragraph error from a missing
/// default, so the defaults are supplied here.
/// </para>
/// <para>
/// Deliberately only <c>display</c>. The rest of a UA stylesheet — heading sizes, list indents,
/// paragraph margins — is left to AngleSharp, and the corpus neutralises what remains of it so a
/// scenario measures layout rather than defaults parity. Closing that gap is worth doing, but it
/// is a separate exercise from making the box tree structurally right.
/// </para>
/// </remarks>
static class UserAgentStyles
{
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
