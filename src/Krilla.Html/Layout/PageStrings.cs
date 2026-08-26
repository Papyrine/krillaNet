/// <summary>
/// The named strings one page sees, ready for <c>string()</c> to read.
/// </summary>
/// <param name="Strings">Every assignment in the document, or null when it makes none.</param>
/// <param name="Top">Where this page's content begins.</param>
/// <param name="End">Where the next page's begins.</param>
/// <remarks>
/// A PAGE's view of <see cref="RunningStrings"/> rather than the strings themselves, because
/// <c>string()</c>'s answer depends on which page is asking — and a margin box is built per page,
/// so binding the page's extent once is less error-prone than threading two floats through every
/// layer that builds one.
///
/// The default is a page that sees nothing, which is what every document declaring no
/// <c>string-set</c> gets and what keeps the parameter optional.
/// </remarks>
readonly record struct PageStrings(RunningStrings? Strings = null, float Top = 0, float End = 0)
{
    /// <summary>What <paramref name="name"/> holds on this page, or the empty string.</summary>
    public string Value(string name) =>
        Strings?.Value(name, Top, End) ?? "";
}
