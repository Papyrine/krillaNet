/// <summary>
/// Where one page's content begins, and what is drawn above it.
/// </summary>
/// <param name="Top">
/// The document position, in layout units, that this page's content starts at.
/// </param>
/// <param name="Reserved">
/// The height of the band at the top of the page that <paramref name="Headers"/> fill, and that
/// the document slice is pushed below. Zero for nearly every page.
/// </param>
/// <param name="Headers">
/// The table header groups re-drawn in that band, outermost table first. Empty for nearly every
/// page.
/// </param>
/// <remarks>
/// <para>
/// This is the whole of what pagination has to say about a page, and it used to be one
/// <c>float</c>. A repeating header is the reason it is not: it takes space at the top of a
/// continuation page, so how much document a page holds is no longer the page's own height, and
/// the position a fragment link resolves to is no longer the document offset alone.
/// </para>
/// <para>
/// The header boxes are the ORIGINALS, shared with the tree rather than copied. They are laid out
/// once, where the table put them, and drawn again through a translate — which is what keeps the
/// repetition a painting concern and leaves every measurement the corpus makes untouched.
/// </para>
/// </remarks>
readonly record struct PageStart(float Top, float Reserved, List<RepeatingHeader> Headers)
{
    /// <summary>A page holding nothing above its content.</summary>
    public static PageStart At(float top) =>
        new(top, 0, []);

    /// <summary>
    /// The page a document position falls on, and how far down that page it lands.
    /// </summary>
    /// <remarks>
    /// The last page starting at or before it. Pages are produced in order, so a reverse scan
    /// finds it without a search structure — and the reserved band has to be added back, or a
    /// fragment link into a table that repeats its header lands above the content it names.
    /// </remarks>
    public static (int Page, float Offset) Locate(List<PageStart> pages, float y)
    {
        var page = pages.Count - 1;

        while (page > 0 && pages[page].Top > y)
        {
            page--;
        }

        return (page, pages[page].Reserved + y - pages[page].Top);
    }
}
