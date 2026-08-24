/// <summary>
/// The casing CSS applies to text before it is shaped.
/// </summary>
/// <remarks>
/// <para>
/// Applied after <see cref="WhiteSpace"/> and before shaping, which is the order that matters for
/// <c>capitalize</c>: a run of white space has already collapsed to one space by the time word
/// boundaries are looked for, so <c>a  b</c> and <c>a b</c> capitalise alike. Shaping afterwards is
/// what makes the transformed text the text that is measured, so a line breaks where the glyphs
/// drawn say it should rather than where the source said.
/// </para>
/// <para>
/// It changes the text and therefore the advances, which is why it cannot be a painting concern:
/// <c>text-transform: uppercase</c> makes a line half again as wide in most faces and wraps a
/// paragraph a line earlier.
/// </para>
/// </remarks>
static class TextTransform
{
    /// <summary>
    /// Applies <paramref name="style"/>'s <c>text-transform</c> to <paramref name="text"/>.
    /// </summary>
    public static string Apply(string text, ComputedStyle style)
    {
        if (text.Length == 0)
        {
            return text;
        }

        return style.TextTransform switch
        {
            TextTransformKind.Uppercase => text.ToUpperInvariant(),
            TextTransformKind.Lowercase => text.ToLowerInvariant(),
            TextTransformKind.Capitalize => Capitalize(text),
            _ => text
        };
    }

    /// <summary>
    /// Upper-cases the first letter of each word.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What counts as the start of a word was measured out of Chrome rather than assumed, because
    /// the plausible answers disagree. <c>page-break o'clock 3rd (bracketed) "quoted"</c> comes
    /// back as <c>Page-Break O'clock 3rd (Bracketed) "Quoted"</c>: a hyphen, a bracket and a quote
    /// each begin a word, and an apostrophe and a digit do not. Reading "a word starts after any
    /// non-letter" gives <c>O'Clock</c> and <c>3Rd</c>, which is what a browser does not do.
    /// </para>
    /// <para>
    /// So the rule is per PRECEDING character: a letter starts a word unless what comes before it
    /// is a letter, a digit, or an apostrophe. That reproduces every case above, and is an
    /// approximation of the UAX #29 word segmentation a browser actually runs — where the
    /// apostrophe is MidLetter and a digit joins the letters around it, which is the same answer
    /// arrived at properly.
    /// </para>
    /// <para>
    /// The boundary is found within one text node. A word split across two inline elements is
    /// capitalised at the start of the second, where a browser looks at the rendered text as a
    /// whole and does not — the same limit <see cref="InlineLayout"/> has to work around for line
    /// breaking, and rarer here, since capitalising mid-word is not something markup normally sets
    /// up.
    /// </para>
    /// </remarks>
    static string Capitalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        var previous = ' ';

        foreach (var character in text)
        {
            builder.Append(
                StartsWord(previous) && char.IsLetter(character)
                    ? char.ToUpperInvariant(character)
                    : character);

            previous = character;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Whether a letter following <paramref name="previous"/> begins a word.
    /// </summary>
    static bool StartsWord(char previous) =>
        !char.IsLetterOrDigit(previous) &&
        previous is not ('\'' or '’');
}
