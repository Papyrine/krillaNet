/// <summary>
/// A stable path identifying an element, used to line our geometry up with the browser's.
/// </summary>
/// <remarks>
/// The format is <c>html &gt; body &gt; div:nth-child(2)</c>. Both this and the script that
/// harvests <c>getBoundingClientRect()</c> in the reference generator build it by the same walk,
/// which is the whole requirement — the string only has to be reproducible, not minimal or
/// pretty, and index-based paths are reproducible in a way that class- or id-based ones are not.
/// </remarks>
static class SelectorPath
{
    /// <summary>Builds the path for <paramref name="element"/>.</summary>
    public static string For(IElement element)
    {
        var segments = new List<string>();

        for (var current = element; current is not null; current = current.ParentElement)
        {
            segments.Add(Segment(current));
        }

        segments.Reverse();
        return string.Join(" > ", segments);
    }

    static string Segment(IElement element)
    {
        var name = element.LocalName;

        if (element.ParentElement is not {} parent)
        {
            return name;
        }

        var index = 1;
        foreach (var sibling in parent.Children)
        {
            if (ReferenceEquals(sibling, element))
            {
                break;
            }

            index++;
        }

        return $"{name}:nth-child({index})";
    }
}