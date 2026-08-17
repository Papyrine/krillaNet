namespace Krilla.Html.Layout;

/// <summary>
/// Decides where the laid-out document is cut into pages.
/// </summary>
/// <remarks>
/// <para>
/// The document is laid out once, as a single column of unbounded height, and then sliced. That
/// ordering is what makes pagination cheap, and it is sound as long as nothing on a page can
/// depend on which page it landed on — true for the current feature set, and the reason running
/// headers, <c>position: fixed</c> and page-relative counters are not in it.
/// </para>
/// <para>
/// Lines are the unbreakable unit. Boxes are not: a background or border spanning a break is
/// clipped at the page edge and resumes on the next page, which is what paged media does with
/// overflow. Honouring <c>break-inside</c> and orphans/widows would mean tracking box-level break
/// opportunities, which slice one does not.
/// </para>
/// </remarks>
static class Paginator
{
    /// <summary>
    /// The Y position, in layout units, where each page's content starts.
    /// </summary>
    /// <remarks>
    /// Always at least one entry, at zero, so an empty document still produces one page rather
    /// than none — an empty PDF is not a valid document, and a blank page is the honest render of
    /// blank input.
    /// </remarks>
    public static List<float> PageTops(LayoutBox root, float pageHeight)
    {
        var tops = new List<float> {0};

        if (pageHeight <= 0)
        {
            return tops;
        }

        var lines = root.Descendants()
            .SelectMany(_ => _.Lines)
            .OrderBy(_ => _.Bounds.Y)
            .ToList();

        var pageTop = 0f;

        foreach (var line in lines)
        {
            if (line.Bounds.Bottom <= pageTop + pageHeight)
            {
                continue;
            }

            // A line taller than the page itself has nowhere better to go. Moving it would put it
            // at the top of the next page and it would still not fit, so it would move again —
            // forever. Let it overflow instead.
            if (line.Bounds.Height > pageHeight)
            {
                continue;
            }

            pageTop = line.Bounds.Y;
            tops.Add(pageTop);
        }

        return tops;
    }

    /// <summary>
    /// The total height of the laid-out document, in layout units.
    /// </summary>
    public static float DocumentHeight(LayoutBox root) =>
        root.BorderBox.Bottom;
}
