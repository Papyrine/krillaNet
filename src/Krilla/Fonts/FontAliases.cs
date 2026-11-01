/// <summary>
/// What a requested family name should be looked for as.
/// </summary>
/// <remarks>
/// <para>
/// Two tables. The first maps the PDF Standard-14 names onto the faces every PDF reader
/// substitutes for them: a document asking for <c>Helvetica</c> means the face Acrobat would
/// draw, which is Arial, and <c>Times-Roman</c> and <c>Courier</c> likewise mean Times New Roman
/// and Courier New. Those names also carry their style — <c>Helvetica-BoldOblique</c> is a family
/// AND a bold italic request — so the style they imply is returned beside the families.
/// </para>
/// <para>
/// The second follows each of those three with its metric-compatible stand-ins: Liberation
/// (what Linux distributions ship) and Croscore's Arimo, Tinos and Cousine. Same advances
/// glyph for glyph, so text laid out for Arial lays out identically in Liberation Sans, and a
/// lookup for Arial on a machine without it finds the face that keeps the layout.
/// </para>
/// <para>
/// The requested name itself always comes first among its substitutes, so a machine that has
/// Arial gets Arial. The Standard-14 names are the exception, being PDF names rather than
/// families: <c>Helvetica</c> resolves to Arial ahead of a real Helvetica, so that the answer
/// is the same on a Mac as on Windows.
/// </para>
/// </remarks>
static class FontAliases
{
    static readonly string[] sans = ["Arial", "Helvetica", "Liberation Sans", "Arimo"];
    static readonly string[] serif = ["Times New Roman", "Times", "Liberation Serif", "Tinos"];
    static readonly string[] mono = ["Courier New", "Courier", "Liberation Mono", "Cousine"];
    static readonly string[] symbol = ["Symbol", "Standard Symbols PS"];
    static readonly string[] dingbats = ["Zapf Dingbats", "ZapfDingbats", "D050000L"];

    static readonly Dictionary<string, (string[] Families, bool Bold, bool Italic)> standard14 =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Helvetica"] = (sans, false, false),
            ["Helvetica-Bold"] = (sans, true, false),
            ["Helvetica-Oblique"] = (sans, false, true),
            ["Helvetica-BoldOblique"] = (sans, true, true),
            ["Times"] = (serif, false, false),
            ["Times Roman"] = (serif, false, false),
            ["Times-Roman"] = (serif, false, false),
            ["Times-Bold"] = (serif, true, false),
            ["Times-Italic"] = (serif, false, true),
            ["Times-BoldItalic"] = (serif, true, true),
            ["Courier"] = (mono, false, false),
            ["Courier-Bold"] = (mono, true, false),
            ["Courier-Oblique"] = (mono, false, true),
            ["Courier-BoldOblique"] = (mono, true, true),
            ["Symbol"] = (symbol, false, false),
            ["ZapfDingbats"] = (dingbats, false, false)
        };

    static readonly Dictionary<string, string[]> substitutes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Arial"] = ["Arial", "Liberation Sans", "Arimo"],
            ["Times New Roman"] = ["Times New Roman", "Liberation Serif", "Tinos"],
            ["Courier New"] = ["Courier New", "Liberation Mono", "Cousine"]
        };

    /// <summary>
    /// The families to try for <paramref name="name"/>, in order, and any style the name itself
    /// implies.
    /// </summary>
    public static (string[] Families, bool Bold, bool Italic) Resolve(string name)
    {
        if (standard14.TryGetValue(name, out var standard))
        {
            return standard;
        }

        if (substitutes.TryGetValue(name, out var families))
        {
            return (families, false, false);
        }

        return ([name], false, false);
    }
}
