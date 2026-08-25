/// <summary>
/// A table header or footer group that is re-drawn on every page its table continues onto.
/// </summary>
/// <param name="Group">The <c>thead</c> or <c>tfoot</c> box, laid out once where the table put it.</param>
/// <param name="Table">
/// The table it belongs to. Carried for two reasons: a COLLAPSED table's grid lines belong to the
/// table rather than to the boxes either side of them, so the group's own rules are not in its
/// subtree; and the band re-drawn on a continuation page reaches to the TABLE's outer edge rather
/// than to the group's.
/// </param>
/// <param name="AtFoot">
/// Whether this is the footer group. The two are mirror images — a band reserved at the bottom of
/// a page rather than at the top, and a translate that puts the group where the page's content
/// ended rather than where it began — which is why one record carries both.
/// </param>
readonly record struct RepeatedRows(LayoutBox Group, LayoutBox Table, bool AtFoot)
{
    /// <summary>
    /// The strip re-drawn on a continuation page: the group, and the table's own edge beyond it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A header's band starts at the TABLE's edge and not at the group's, which is a difference of
    /// half a rule under the collapsing model and of a border plus <c>border-spacing</c> under the
    /// separated one. Measured: Chromium lands the first continued row 61.5px down a page whose
    /// header is 61px tall, and the half pixel is the top rule the table draws above it. Reserving
    /// the group's own height instead puts every horizontal rule on the page one device pixel high.
    /// A footer's band reaches to the table's bottom edge for the same reason.
    /// </para>
    /// <para>
    /// A CAPTION is not repeated, so the band stops short of one. Without that a captioned table
    /// would carry the caption's height as blank space on every page it continues onto.
    /// </para>
    /// </remarks>
    public Rect Band
    {
        get
        {
            var top = AtFoot ? Group.BorderBox.Y : Table.BorderBox.Y;
            var bottom = AtFoot ? Table.BorderBox.Bottom : Group.BorderBox.Bottom;

            foreach (var child in Table.Children)
            {
                if (child.Style.Display != DisplayKind.TableCaption)
                {
                    continue;
                }

                if (AtFoot)
                {
                    if (child.BorderBox.Y >= Group.BorderBox.Bottom)
                    {
                        bottom = Math.Min(bottom, child.BorderBox.Y);
                    }
                }
                else if (child.BorderBox.Bottom <= Group.BorderBox.Y)
                {
                    top = Math.Max(top, child.BorderBox.Bottom);
                }
            }

            return new(
                Table.BorderBox.X,
                top,
                Table.BorderBox.Width,
                Math.Max(0, bottom - top));
        }
    }

    /// <summary>Where the table begins. Before it there is nothing yet to label or total.</summary>
    public float TableTop => Table.BorderBox.Y;

    /// <summary>
    /// Where the table ends. Past it there is nothing left to label, so the group stops
    /// repeating — a page holding the paragraph AFTER a table should not carry that table's
    /// column headings.
    /// </summary>
    public float TableBottom => Table.BorderBox.Bottom;
}
