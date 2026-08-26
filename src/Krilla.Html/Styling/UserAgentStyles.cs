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
    /// Only the properties this engine honours are listed.
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

        /*
          The HTML 4.01 PRINT stylesheet, neutralised. It is the sample sheet from that
          specification's appendix rather than anything a browser implements, and resolving media
          against the printer — which a PDF converter must — brings the whole of it in:

            h1              { page-break-before: always }
            h1, h2, ..., h6 { page-break-after: avoid }
            ul, ol, dl      { page-break-before: avoid }

          The first is the one that matters. No browser starts a new page before every <h1>, and
          the corpus said so within a second of print media being switched on: `ua/headings`
          grew a second page against a reference that has one. The other two are unobservable in a
          document that fits on a page, and would report on every heading and every list in every
          document converted — which is the noise the diagnostic table exists to avoid.

          Restated as `auto` rather than removed, because AngleSharp's sheet cannot be edited: a
          later user-agent rule of equal specificity is how anything here overrides it, and an
          author rule still beats this.
        */
        h1 { page-break-before: auto; }
        h1, h2, h3, h4, h5, h6 { page-break-after: auto; }
        ul, ol, dl { page-break-before: auto; }

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
          The marker a list shows, and how it changes with depth. Declared on the list rather than
          on the item because that is where the HTML Standard puts it and where an author expects
          to override it; `list-style-type` inherits, so it reaches the items either way.

          Only unordered nesting cycles. An ordered list stays decimal however deep it is, which is
          why there is no matching ol chain here.
        */
        ul, menu { list-style-type: disc; }
        ol { list-style-type: decimal; }
        ul ul, ul menu, ol ul, ol menu, menu ul, menu menu { list-style-type: circle; }
        ul ul ul, ul ol ul, ol ul ul, ol ol ul, ul ul menu, ol ul menu { list-style-type: square; }

        /*
          Only an anchor WITH an href is underlined. A bare <a> is a target rather than a link,
          and browsers leave it undecorated.
        */
        a[href] { text-decoration: underline; }

        code, kbd, samp { font-family: monospace; }
        center { text-align: center; }

        /*
          The table defaults. AngleSharp already supplies the display roles and the 2px
          border-spacing; what it omits is the one-pixel cell padding, without which every cell is
          two pixels narrower and two shorter than a browser draws it — small enough to look like
          rounding and large enough to move every column.

          And `box-sizing`, which is where a table's declared width including its border comes
          from — the user-agent rule, not the table algorithm. Measured: `box-sizing: content-box`
          on a table with a border makes Chrome lay it out the other way, which it could not do if
          the rule were part of table layout itself.
        */
        table { box-sizing: border-box; }
        td, th { padding: 1px; }
        th { font-weight: bold; text-align: center; }
        caption { text-align: center; }
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
    ///
    /// The obsolete presentational elements are here too, and they are not decoration: a
    /// <c>&lt;font&gt;</c> laid out as a block puts every run it wraps on a line of its own, which
    /// is a whole-document difference in exactly the documents that still contain one. Found by
    /// applying the presentational attributes and watching six <c>&lt;font size&gt;</c> spans come
    /// out on six separate lines.
    /// </remarks>
    static readonly HashSet<string> inline =
    [
        with(StringComparer.OrdinalIgnoreCase),
        "a", "abbr", "acronym", "b", "bdi", "bdo", "big", "br", "cite", "code", "data", "dfn",
        "em", "font", "i", "img", "kbd", "label", "mark", "nobr", "q", "rp", "rt", "ruby", "s",
        "samp", "small", "span", "strike", "strong", "sub", "sup", "time", "tt", "u", "var", "wbr"
    ];

    /// <summary>
    /// Elements that generate no box, whatever their content.
    /// </summary>
    /// <remarks>
    /// A safety net rather than the primary mechanism — AngleSharp's default sheet already hides
    /// most of these. Without it a stylesheet's own text would be laid out as content, which is
    /// both wrong and extremely obvious.
    /// </remarks>
    static readonly HashSet<string> hidden =
    [
        with(StringComparer.OrdinalIgnoreCase),
        "head", "link", "meta", "script", "style", "title", "base", "template"
    ];

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

        if (inline.Contains(localName))
        {
            return DisplayKind.Inline;
        }

        return null;
    }

    /// <summary>Whether <paramref name="localName"/> forces a line break.</summary>
    public static bool IsLineBreak(string localName) =>
        localName.Equals("br", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether <paramref name="localName"/> offers a line break without forcing one.
    /// </summary>
    /// <remarks>
    /// <c>&lt;wbr&gt;</c> is the only one, and it is the only way HTML has of saying "this word may
    /// be split here", which is why a document holding a long URL or an identifier reaches for it.
    /// </remarks>
    public static bool IsWordBreak(string localName) =>
        localName.Equals("wbr", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The <c>vertical-align</c> a table element starts from, or null to inherit.
    /// </summary>
    /// <remarks>
    /// The user-agent sheet puts <c>middle</c> on the table and <c>inherit</c> on everything under
    /// it, so a cell ends up middle-aligned unless a row or the table says otherwise. Supplied here
    /// rather than as CSS because <c>inherit</c> as a declared keyword is not implemented, and
    /// seeding the root of the table has the same effect for the one property that needs it.
    /// </remarks>
    public static VerticalAlignKind? DefaultVerticalAlign(string localName)
    {
        if (localName.Equals("table", StringComparison.OrdinalIgnoreCase))
        {
            return VerticalAlignKind.Middle;
        }

        return null;
    }

    /// <summary>Whether <paramref name="localName"/> is bold by default.</summary>
    public static bool IsBold(string localName) =>
        localName is "b" or "strong" or "th";

    /// <summary>Whether <paramref name="localName"/> is italic by default.</summary>
    public static bool IsItalic(string localName) =>
        localName is "i" or "em" or "cite" or "var" or "dfn" or "address";
}
