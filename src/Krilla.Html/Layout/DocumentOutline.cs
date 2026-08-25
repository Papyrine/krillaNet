namespace Krilla.Html.Layout;

/// <summary>
/// Builds the PDF outline — the bookmark tree a reader shows in its sidebar — from the document's
/// headings.
/// </summary>
/// <remarks>
/// <para>
/// From <c>h1</c> to <c>h6</c>, nested by level. A browser printing to PDF produces no outline at
/// all, so this is the one thing here that is not a fidelity matter: it adds nothing to the page and
/// takes nothing away, and a fifty-page report without bookmarks is markedly harder to read than one
/// with them. That is why it is on by default where the two pagination switches are off — nothing it
/// does can disagree with the reference.
/// </para>
/// <para>
/// Levels rather than depth of nesting, so a document that goes from <c>h1</c> to <c>h3</c> without
/// an <c>h2</c> produces a two-level tree rather than a broken one. The stack is what carries that:
/// a heading attaches to the nearest open heading of a SMALLER level, whatever the gap.
/// </para>
/// </remarks>
static class DocumentOutline
{
    /// <summary>
    /// The outline for <paramref name="root"/>, or an empty list when there is nothing to build one
    /// from.
    /// </summary>
    /// <param name="root">The laid-out tree.</param>
    /// <param name="pages">Where each page's content starts, in layout units.</param>
    /// <param name="content">The page content box, in layout units.</param>
    /// <param name="scale">Points per layout unit.</param>
    /// <param name="depth">The deepest heading level to include, 1 to 6. Zero produces nothing.</param>
    public static List<OutlineItem> Build(
        LayoutBox root,
        List<PageStart> pages,
        Rect content,
        float scale,
        int depth)
    {
        var items = new List<OutlineItem>();

        if (depth <= 0)
        {
            return items;
        }

        // The open ancestors, innermost last, each with the heading level it came from.
        var open = new List<(int Level, OutlineItem Item)>();

        foreach (var box in root.Descendants())
        {
            if (Level(box) is not {} level || level > depth)
            {
                continue;
            }

            if (Title(box) is not {} title)
            {
                continue;
            }

            var (page, target) = Position(box, pages, content, scale);
            var item = new OutlineItem(title, page, target);

            // Pop back to the nearest open heading of a smaller level. A run of equal or deeper
            // levels above this one is closed by the same test, which is what makes h3 after h3 a
            // sibling and h3 after h1 a child.
            while (open.Count > 0 &&
                   open[^1].Level >= level)
            {
                open.RemoveAt(open.Count - 1);
            }

            if (open.Count > 0)
            {
                open[^1].Item.Add(item);
            }
            else
            {
                items.Add(item);
            }

            open.Add((level, item));
        }

        return items;
    }

    /// <summary>The heading level of a box, or null when it is not a heading.</summary>
    /// <remarks>
    /// By element name rather than by <c>display</c> or font size, because a heading is a document
    /// structure rather than an appearance — a styled <c>div</c> that looks like a heading is not
    /// one, and an <c>h2</c> restyled to look like body text still is.
    /// </remarks>
    static int? Level(LayoutBox box) =>
        box.Element?.LocalName switch
        {
            "h1" => 1,
            "h2" => 2,
            "h3" => 3,
            "h4" => 4,
            "h5" => 5,
            "h6" => 6,
            _ => null
        };

    /// <summary>
    /// A heading's text, collapsed to one line, or null when it has none.
    /// </summary>
    /// <remarks>
    /// Taken from the element rather than from the laid-out runs, which would have to be gathered
    /// across lines and would pick up generated content — a numbering <c>::before</c> belongs in the
    /// bookmark, but reassembling it from runs in the right order is work for no gain over reading
    /// the source. A heading with no text at all is skipped rather than given an empty bookmark,
    /// which a reader shows as an unclickable blank row.
    /// </remarks>
    static string? Title(LayoutBox box)
    {
        if (box.Element?.TextContent is not { } text)
        {
            return null;
        }

        var split = text.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 0)
        {
            return null;
        }

        return string.Join(' ', split);
    }

    /// <summary>
    /// The page a box landed on, and its position in that page's own space, in PDF points.
    /// </summary>
    /// <remarks>
    /// The same arithmetic <see cref="LinkTargets"/> does, and for the same reason: a bookmark and
    /// an internal link are the same thing to a PDF — a page and a point on it.
    /// </remarks>
    public static (int Page, Point Target) Position(
        LayoutBox box,
        List<PageStart> pages,
        Rect content,
        float scale)
    {
        var (page, offset) = PageStart.Locate(pages, box.BorderBox.Y);

        return (
            page,
            new(
                (content.X + box.BorderBox.X) * scale,
                (content.Y + offset) * scale));
    }
}
