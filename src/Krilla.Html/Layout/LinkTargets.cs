/// <summary>
/// Where each <c>id</c> in the document ended up, so a <c>#fragment</c> link can point at it.
/// </summary>
/// <remarks>
/// <para>
/// Built after pagination, because that is the earliest it can be. A fragment names an element,
/// and a PDF internal link names a page and a point on it — the mapping between the two only exists
/// once the document has been cut into pages.
/// </para>
/// <para>
/// Coordinates come out in PDF points, in the destination page's own space, which is what
/// <see cref="Surface.AddLink(Rectangle, int, Point)"/> takes.
/// </para>
/// </remarks>
sealed class LinkTargets
{
    readonly Dictionary<string, (int Page, Point Target)> targets = new(StringComparer.Ordinal);

    LinkTargets()
    {
    }

    /// <summary>
    /// Maps every element carrying an <c>id</c> to the page it landed on.
    /// </summary>
    /// <param name="root">The laid-out tree.</param>
    /// <param name="pageTops">Where each page's content starts, in layout units.</param>
    /// <param name="content">The page content box, in layout units.</param>
    /// <param name="scale">Points per layout unit.</param>
    public static LinkTargets Build(
        LayoutBox root,
        List<float> pageTops,
        Rect content,
        float scale)
    {
        var targets = new LinkTargets();

        foreach (var box in root.Descendants())
        {
            if (box.Element?.Id is not {Length: > 0} id || targets.targets.ContainsKey(id))
            {
                continue;
            }

            // The page a position falls on is the last one starting at or before it. Pages are
            // produced in order, so a reverse scan finds it without a search structure.
            var page = pageTops.Count - 1;
            while (page > 0 && pageTops[page] > box.BorderBox.Y)
            {
                page--;
            }

            targets.targets[id] = (
                page,
                new(
                    (content.X + box.BorderBox.X) * scale,
                    (content.Y + box.BorderBox.Y - pageTops[page]) * scale));
        }

        return targets;
    }

    /// <summary>
    /// Resolves a fragment, or returns false when nothing in the document carries that id.
    /// </summary>
    /// <remarks>
    /// An unresolved fragment produces no annotation at all rather than one pointing at page one.
    /// A link that silently goes to the wrong place is worse than one that is not there.
    /// </remarks>
    public bool TryResolve(string fragment, out int page, out Point target)
    {
        if (targets.TryGetValue(fragment, out var found))
        {
            (page, target) = found;
            return true;
        }

        page = 0;
        target = default;
        return false;
    }
}
