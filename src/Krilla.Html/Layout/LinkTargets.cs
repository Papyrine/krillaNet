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
    /// <param name="pages">Where each page's content starts, in layout units.</param>
    /// <param name="content">The page content box, in layout units.</param>
    /// <param name="scale">Points per layout unit.</param>
    public static LinkTargets Build(
        LayoutBox root,
        List<PageStart> pages,
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

            var (page, offset) = PageStart.Locate(pages, box.BorderBox.Y);

            targets.targets[id] = (
                page,
                new(
                    (content.X + box.BorderBox.X) * scale,
                    (content.Y + offset) * scale));
        }

        return targets;
    }

    /// <summary>
    /// Every id in the document with the page and point it landed on.
    /// </summary>
    /// <remarks>
    /// Read to register PDF named destinations, which are the same mapping viewed from outside the
    /// document: a <c>#fragment</c> link resolves one internally, and a named destination lets a URL
    /// resolve the same one from another file.
    /// </remarks>
    public IEnumerable<(string Name, int Page, Point Target)> All() =>
        targets.Select(_ => (_.Key, _.Value.Page, _.Value.Target));

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
