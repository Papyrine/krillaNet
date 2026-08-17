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

        var documentHeight = Math.Max(
            root.BorderBox.Bottom,
            lines.Count == 0 ? 0 : lines.Max(_ => _.Bounds.Bottom));

        var top = 0f;

        while (top + pageHeight < documentHeight)
        {
            top = NextTop(lines, top, pageHeight);
            tops.Add(top);
        }

        return tops;
    }

    /// <summary>
    /// Where the page after the one starting at <paramref name="top"/> begins.
    /// </summary>
    /// <remarks>
    /// A straddling line moves whole to the next page, so the break goes at its top. When nothing
    /// straddles the boundary the break goes at the page edge itself — and that fallback is not a
    /// degenerate case, it is what carries a block taller than the page onto the next one. A tall
    /// <c>div</c> contains no lines at all, so there is no line-based candidate anywhere inside it;
    /// without the fallback the break lands after the whole block and everything between the page
    /// edge and the block's end is simply never drawn.
    /// </remarks>
    static float NextTop(List<LineBox> lines, float top, float pageHeight)
    {
        var limit = top + pageHeight;

        foreach (var line in lines)
        {
            // Lines already on this page or an earlier one.
            if (line.Bounds.Y <= top || line.Bounds.Bottom <= limit)
            {
                continue;
            }

            // Beyond the boundary entirely: nothing straddles it, so the page ends at its edge.
            if (line.Bounds.Y > limit)
            {
                break;
            }

            // A line taller than the page has nowhere better to go — moving it to the top of the
            // next page would leave it still not fitting, and it would move again forever. Let it
            // overflow and keep looking for a line that can actually be moved.
            if (line.Bounds.Height > pageHeight)
            {
                continue;
            }

            return line.Bounds.Y;
        }

        return limit;
    }

    /// <summary>
    /// The total height of the laid-out document, in layout units.
    /// </summary>
    public static float DocumentHeight(LayoutBox root) =>
        root.BorderBox.Bottom;
}
