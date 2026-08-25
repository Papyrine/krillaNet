/// <summary>
/// Which named page each sheet belongs to, from the <c>page</c> property.
/// </summary>
/// <remarks>
/// <para>
/// CSS Paged Media's other half: <c>page: cover</c> on an element puts the sheets that element
/// occupies into a named page, which <c>@page cover</c> then styles. It is how a document gives
/// its cover a different header from its body without a second stylesheet.
/// </para>
/// <para>
/// Matched by EXTENT rather than by assignment, which is what makes it behave as CSS asks without
/// any inheritance of its own: a page belongs to the innermost box whose own extent covers where
/// that page begins, so every descendant of a named box is on named pages for as long as the box
/// lasts and no longer.
/// </para>
/// <para>
/// The name cannot change the page's GEOMETRY, which is the same limitation a pseudo-class carries
/// here: the document is laid out once, against one rectangle, so a rule whose margins differ from
/// the rest is read for its margin boxes and not for its margins.
/// </para>
/// </remarks>
sealed class PageNames
{
    readonly List<(float Top, float Bottom, string Name)> extents = [];

    /// <summary>Collects every named extent in the laid-out tree, outermost first.</summary>
    /// <remarks>
    /// Pre-order, which is what settles nesting: a box inside a named one is visited later, so the
    /// last match is the innermost and the lookup below simply keeps overwriting.
    /// </remarks>
    public static PageNames Build(LayoutBox root)
    {
        var names = new PageNames();

        foreach (var box in root.Descendants())
        {
            if (box.Style.PageName is {Length: > 0} name)
            {
                names.extents.Add((box.BorderBox.Y, box.BorderBox.Bottom, name));
            }
        }

        return names;
    }

    /// <summary>The name of the page beginning at <paramref name="top"/>, or null.</summary>
    public string? Value(float top)
    {
        string? found = null;

        foreach (var (from, to, name) in extents)
        {
            if (from <= top && to > top)
            {
                found = name;
            }
        }

        return found;
    }
}
