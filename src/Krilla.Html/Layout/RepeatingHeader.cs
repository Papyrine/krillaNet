/// <summary>
/// A table header group that is re-drawn at the top of every page its table continues onto.
/// </summary>
/// <param name="Group">The <c>thead</c> box, laid out once where the table put it.</param>
/// <param name="Table">
/// The table it belongs to. Carried for two reasons: a COLLAPSED table's grid lines belong to the
/// table rather than to the boxes either side of them, so the header's own rules are not in its
/// subtree; and the band re-drawn on a continuation page starts at the TABLE's top edge rather
/// than at the header's.
/// </param>
readonly record struct RepeatingHeader(LayoutBox Group, LayoutBox Table)
{
    /// <summary>
    /// The strip re-drawn at the top of a continuation page: the table's own top edge, and the
    /// header down to its last rule.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It starts at the table's edge and not at the header's, which is a difference of half a rule
    /// under the collapsing model and of a border plus <c>border-spacing</c> under the separated
    /// one. Measured: Chromium lands the first continued row 61.5px down a page whose header is
    /// 61px tall, and the half pixel is the top rule the table draws above it. Reserving the
    /// header's own height instead puts every horizontal rule on the page one device pixel high.
    /// </para>
    /// <para>
    /// A CAPTION is not repeated, so the band starts below one. Without that a captioned table
    /// would carry the caption's height as blank space at the top of every continuation page.
    /// </para>
    /// </remarks>
    public Rect Band
    {
        get
        {
            var top = Table.BorderBox.Y;

            foreach (var child in Table.Children)
            {
                if (child.Style.Display == DisplayKind.TableCaption &&
                    child.BorderBox.Bottom <= Group.BorderBox.Y)
                {
                    top = Math.Max(top, child.BorderBox.Bottom);
                }
            }

            return new(
                Table.BorderBox.X,
                top,
                Table.BorderBox.Width,
                Math.Max(0, Group.BorderBox.Bottom - top));
        }
    }

    /// <summary>
    /// Where the table ends. Past it there is nothing left to label, so the header stops
    /// repeating — a page holding the paragraph AFTER a table should not carry that table's
    /// column headings.
    /// </summary>
    public float TableBottom => Table.BorderBox.Bottom;
}
