/// <summary>
/// The named strings a document sets with <c>string-set</c>, and what each holds on a given page.
/// </summary>
/// <remarks>
/// <para>
/// CSS Generated Content for Paged Media's own running-header mechanism, and the reason most
/// documents have an <c>@page</c> rule at all: <c>h2 { string-set: title content() }</c> paired
/// with <c>@top-center { content: string(title) }</c> puts the current section's heading at the
/// top of every sheet.
/// </para>
/// <para>
/// Built AFTER layout, because a named string's value is a function of where the page boundaries
/// fell — the same element sets it on every page it precedes, and which of those pages is asking
/// decides the answer.
/// </para>
/// </remarks>
sealed class RunningStrings
{
    readonly List<(float Y, string Name, string Value)> entries = [];

    /// <summary>Collects every assignment in the laid-out tree, in document order.</summary>
    /// <remarks>
    /// Sorted by position rather than left in tree order, which are the same sequence for a
    /// document in normal flow and are not for one with floats or absolute boxes in it. The lookup
    /// below walks in page order, so position is the order that matters.
    /// </remarks>
    public static RunningStrings Build(LayoutBox root)
    {
        var strings = new RunningStrings();

        foreach (var box in root.Descendants())
        {
            if (box.Strings is not {Count: > 0} declared)
            {
                continue;
            }

            foreach (var (name, value) in declared)
            {
                strings.entries.Add((box.BorderBox.Y, name, value));
            }
        }

        strings.entries.Sort((left, right) => left.Y.CompareTo(right.Y));
        return strings;
    }

    /// <summary>Whether the document sets any named string at all.</summary>
    public bool Any => entries.Count > 0;

    /// <summary>
    /// What <paramref name="name"/> holds on the page running from <paramref name="top"/> to
    /// <paramref name="end"/>.
    /// </summary>
    /// <remarks>
    /// The <c>first</c> rule, which is <c>string()</c>'s default: the value assigned by the FIRST
    /// element on the page that sets it, and otherwise whatever was carried forward from before the
    /// page began. That is what makes a running header name the section a page starts in rather
    /// than the one it ends in — a page whose first heading is "Materials" is headed "Materials",
    /// and a page holding no heading at all keeps the previous one.
    /// </remarks>
    public string Value(string name, float top, float end)
    {
        var carried = "";

        foreach (var (y, key, value) in entries)
        {
            if (!key.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (y < top)
            {
                carried = value;
                continue;
            }

            if (y < end)
            {
                return value;
            }

            break;
        }

        return carried;
    }
}
