/// <summary>
/// Numbers the items of one list.
/// </summary>
/// <remarks>
/// Held by the box builder while it walks a list's children, rather than derived per item from the
/// DOM, so that an item's number follows from the items actually generated before it. That is what
/// makes a <c>display: none</c> item skip a number rather than consume one, and it is why the
/// counter is a mutable object passed down instead of a function of the element.
/// </remarks>
sealed class ListNumbering
{
    readonly int step;
    int next;

    ListNumbering(int first, int step)
    {
        next = first;
        this.step = step;
    }

    /// <summary>
    /// The numbering <paramref name="list"/>'s items take, honouring <c>start</c> and
    /// <c>reversed</c>.
    /// </summary>
    /// <remarks>
    /// A reversed list with no <c>start</c> counts down from the number of items it has, which is
    /// the only reason the items need counting before any of them is laid out.
    /// </remarks>
    public static ListNumbering For(IElement list)
    {
        var reversed = list.HasAttribute("reversed");
        var step = reversed ? -1 : 1;

        if (Number(list, "start") is {} start)
        {
            return new(start, step);
        }

        return new(reversed ? list.Children.Count(IsItem) : 1, step);
    }

    /// <summary>
    /// The number for <paramref name="item"/>, advancing the counter past it.
    /// </summary>
    /// <remarks>
    /// A <c>value</c> attribute does not just override this item's number: it moves the counter, so
    /// every item after it continues from there. That is what the HTML Standard specifies and what
    /// makes <c>value</c> usable for resuming an interrupted list.
    /// </remarks>
    public int Take(IElement item)
    {
        if (Number(item, "value") is {} value)
        {
            next = value;
        }

        var ordinal = next;
        next += step;
        return ordinal;
    }

    static bool IsItem(IElement element) =>
        element.LocalName.Equals("li", StringComparison.OrdinalIgnoreCase);

    static int? Number(IElement element, string name)
    {
        if (int.TryParse(
                element.GetAttribute(name),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var value))
        {
            return value;
        }

        return null;
    }
}