namespace Krilla.Html;

/// <summary>
/// What the engine cannot do to a run of TEXT, as opposed to what it cannot do with a declaration.
/// </summary>
/// <remarks>
/// <para>
/// <c>UnsupportedCss</c> scans the cascade and <c>UnsupportedAttributes</c> the markup, and between
/// them they carry the invariant that a conversion reporting nothing rendered everything the way a
/// browser would. That invariant was false for a whole class of gap: line breaking, bidirectional
/// reordering and font coverage are properties of the CHARACTERS, so no amount of scanning a
/// stylesheet finds them and a document in Arabic or Japanese converted silently and wrongly.
/// </para>
/// <para>
/// The three reports below are about what is MISSING from the engine rather than about the script
/// itself, so each disappears the day the gap is closed. Each fires at most once per run of text,
/// and — like every other scan here — only when the caller is listening.
/// </para>
/// </remarks>
static class UnsupportedText
{
    /// <summary>
    /// Reports whatever about <paramref name="text"/> this engine will get wrong.
    /// </summary>
    public static void Report(
        Action<HtmlDiagnostic>? sink,
        FontSet? fonts,
        string element,
        string text)
    {
        if (sink is null)
        {
            return;
        }

        var unbreakable = false;
        var reordered = false;
        var uncovered = false;

        foreach (var rune in text.EnumerateRunes())
        {
            var value = rune.Value;

            if (!unbreakable && NeedsWordBreaking(value))
            {
                unbreakable = true;

                Diagnostic.Text(
                    sink,
                    element,
                    "line breaking",
                    Describe(rune),
                    "no break opportunity is offered inside this script, so a line of it " +
                    "overflows rather than wrapping");
            }

            if (!reordered && IsRightToLeft(value))
            {
                reordered = true;

                Diagnostic.Text(
                    sink,
                    element,
                    "bidirectional text",
                    Describe(rune),
                    "laid out left to right, so it comes out in the wrong order");
            }

            if (!uncovered && fonts is not null && Drawn(rune) && !fonts.AnyCovers(value))
            {
                uncovered = true;

                Diagnostic.Text(
                    sink,
                    element,
                    "font coverage",
                    Describe(rune),
                    "no registered font covers it, so it is drawn as .notdef");
            }

            if (unbreakable && reordered && uncovered)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Whether a line may break INSIDE a run of this script, with no space to break at.
    /// </summary>
    /// <remarks>
    /// The engine's break opportunities are spaces, hyphens and dashes, either side of an atomic
    /// inline, and the cuts <c>word-break</c> asks for. The scripts below write without spaces, so
    /// a paragraph of any of them is one unbreakable word to this engine and overflows the page.
    /// The ranges are the Unicode BLOCKS rather than a full UAX #14 class table, which would be the
    /// implementation rather than the report.
    /// </remarks>
    static bool NeedsWordBreaking(int value) =>
        value is
            >= 0x0E00 and <= 0x0EFF or // Thai and Lao
            >= 0x1000 and <= 0x109F or // Myanmar
            >= 0x1780 and <= 0x17FF or // Khmer
            >= 0x2E80 and <= 0x303F or // CJK radicals, Kangxi, CJK symbols and punctuation
            >= 0x3040 and <= 0x30FF or // Hiragana and Katakana
            >= 0x3400 and <= 0x4DBF or // CJK unified ideographs extension A
            >= 0x4E00 and <= 0x9FFF or // CJK unified ideographs
            >= 0xAC00 and <= 0xD7AF or // Hangul syllables
            >= 0xF900 and <= 0xFAFF or // CJK compatibility ideographs
            >= 0x20000 and <= 0x323AF; // The supplementary ideograph planes

    /// <summary>
    /// Whether this character is written right to left, so that a run holding it needs reordering.
    /// </summary>
    /// <remarks>
    /// The strong RTL blocks, which is what a report needs: a neutral or a Latin digit among them
    /// is reordered too, but only because something strong is there to reorder it, and one report
    /// per run is the point.
    /// </remarks>
    static bool IsRightToLeft(int value) =>
        value is
            >= 0x0590 and <= 0x05FF or // Hebrew
            >= 0x0600 and <= 0x06FF or // Arabic
            >= 0x0700 and <= 0x074F or // Syriac
            >= 0x0780 and <= 0x07BF or // Thaana
            >= 0x07C0 and <= 0x07FF or // NKo
            >= 0x0800 and <= 0x085F or // Samaritan and Mandaic
            >= 0x08A0 and <= 0x08FF or // Arabic extended-A
            >= 0xFB1D and <= 0xFDFF or // Hebrew and Arabic presentation forms A
            >= 0xFE70 and <= 0xFEFF; // Arabic presentation forms B

    /// <summary>
    /// Whether this character reaches a font at all, so that its absence from one is worth saying.
    /// </summary>
    /// <remarks>
    /// A tab, a newline and a soft hyphen are not glyphs — the first two are handled by white-space
    /// processing and the third is stripped before shaping — so no face covers any of them and
    /// none of them is missing. Found by the corpus, which reported four scenarios the moment the
    /// coverage check went in: <c>text/tabs</c>, both <c>inline/white_space</c> rows and
    /// <c>inline/white_space_pre</c>, every one of which is pixel-identical to Chrome.
    /// </remarks>
    static bool Drawn(Rune rune) =>
        !Rune.IsWhiteSpace(rune) &&
        !Rune.IsControl(rune) &&
        Rune.GetUnicodeCategory(rune) is not (
            UnicodeCategory.Format or
            UnicodeCategory.Surrogate or
            UnicodeCategory.PrivateUse);

    /// <summary>
    /// The character itself and its code point, since one of the two is unreadable in a log and the
    /// other is unsearchable.
    /// </summary>
    static string Describe(Rune rune) =>
        $"{rune} (U+{rune.Value:X4})";
}
