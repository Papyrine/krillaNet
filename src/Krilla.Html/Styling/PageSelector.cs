namespace Krilla.Html.Styling;

/// <summary>
/// The pages an <c>@page</c> rule applies to.
/// </summary>
/// <remarks>
/// Flags rather than one value, because the pseudo-classes combine: <c>@page :first:right</c> is
/// legal and names one page of a document. <see cref="All"/> is the absence of any, which is the
/// rule an author writes first and the one everything else refines.
/// </remarks>
[Flags]
enum PageSelector
{
    /// <summary>Every page.</summary>
    All = 0,

    /// <summary>The first page of the document.</summary>
    First = 1,

    /// <summary>A left-hand sheet, which is an even-numbered page.</summary>
    Left = 2,

    /// <summary>A right-hand sheet. Page one is one, which is what makes odd pages right.</summary>
    Right = 4,

    /// <summary>A page a forced break left empty.</summary>
    Blank = 8,

    /// <summary>
    /// No page at all, for a selector that was read and cannot be honoured.
    /// </summary>
    /// <remarks>
    /// A named page — <c>@page cover</c> — selects the elements carrying <c>page: cover</c>, which
    /// this engine does not read. Dropping the rule is the conservative answer: applied to every
    /// page instead, a cover sheet's header would appear on all of them, which is worse than the
    /// header being absent and much harder to attribute.
    /// </remarks>
    Never = 16
}

/// <summary>
/// One page margin box, as declared.
/// </summary>
/// <param name="Selector">The pages it applies to.</param>
/// <param name="Order">
/// Its position in the document's stylesheets, which settles a tie between two rules of equal
/// specificity the way a later rule winning does everywhere else in CSS.
/// </param>
/// <param name="Slot">Which of the sixteen boxes it fills.</param>
/// <param name="Declarations">
/// The text between its braces, kept as written. Parsed once per page it appears on rather than
/// here, because a margin box may name <c>counter(page)</c> and so has a different answer on each.
/// </param>
readonly record struct PageMarginRule(
    PageSelector Selector,
    int Order,
    PageMarginSlot Slot,
    string Declarations)
{
    /// <summary>
    /// How specific the selector is, in CSS Paged Media's own order.
    /// </summary>
    /// <remarks>
    /// <c>:first</c> and <c>:blank</c> outrank <c>:left</c> and <c>:right</c>, which outrank no
    /// selector at all — so <c>@page :first</c> beats <c>@page :right</c> on the one page that is
    /// both, which is the ordering every "no header on the title page" stylesheet depends on.
    /// </remarks>
    public int Specificity =>
        (Selector.HasFlag(PageSelector.First) ? 2 : 0) +
        (Selector.HasFlag(PageSelector.Blank) ? 2 : 0) +
        (Selector.HasFlag(PageSelector.Left) ? 1 : 0) +
        (Selector.HasFlag(PageSelector.Right) ? 1 : 0);

    /// <summary>
    /// Whether this rule applies to a page.
    /// </summary>
    /// <param name="number">The page's number, from one.</param>
    /// <param name="blank">Whether a forced break left it empty.</param>
    public bool Matches(int number, bool blank)
    {
        if (Selector.HasFlag(PageSelector.Never))
        {
            return false;
        }

        if (Selector.HasFlag(PageSelector.First) && number != 1)
        {
            return false;
        }

        if (Selector.HasFlag(PageSelector.Blank) && !blank)
        {
            return false;
        }

        if (Selector.HasFlag(PageSelector.Left) && number % 2 == 1)
        {
            return false;
        }

        return !Selector.HasFlag(PageSelector.Right) || number % 2 != 0;
    }
}
