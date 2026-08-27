namespace Krilla.Html.Structure;

/// <summary>
/// What a table's cells say about their own shape: how far each spans, and which header cells
/// describe it.
/// </summary>
/// <remarks>
/// <para>
/// A span is read from the LAYOUT rather than off the attribute, and the two are not the same
/// number. <c>rowspan="0"</c> means "to the end of the rows", a span reaching past the last row is
/// clamped to it, and both are resolved by <see cref="TableGrid"/> while the table is placed — so
/// reading the attribute here would let a reader be told a cell covers three rows while the page
/// shows it covering two, which is the one thing a structure tree must not do. Building the grid a
/// second time is what keeps the two answers the same one: it is a pure function of the box tree,
/// so it cannot drift from what layout did.
/// </para>
/// <para>
/// A header association is the other way round, and is not a layout fact at all. HTML's
/// <c>headers</c> attribute names header cells by id, which is the author saying explicitly what
/// <c>scope</c> can only imply, and PDF wants exactly that pair — an <c>/ID</c> on the header and
/// a <c>/Headers</c> array on the cell naming it. A reference is resolved to an ELEMENT rather
/// than kept as the string, so a document with two elements sharing an id publishes the id once,
/// on the one <c>getElementById</c> actually reaches.
/// </para>
/// </remarks>
sealed class TableAssociations
{
    readonly Dictionary<string, (int Rows, int Columns)> spans = new(StringComparer.Ordinal);
    readonly Dictionary<string, List<IElement>> headers = new(StringComparer.Ordinal);
    readonly HashSet<string> targets = new(StringComparer.Ordinal);

    /// <summary>Every cell in every table under <paramref name="root"/>.</summary>
    public static TableAssociations Build(LayoutBox root)
    {
        var associations = new TableAssociations();
        var cells = new List<IElement>();

        foreach (var box in root.Descendants())
        {
            if (box.Style.Display != DisplayKind.Table)
            {
                continue;
            }

            foreach (var cell in TableGrid.Build(box).Cells)
            {
                if (cell.Box.Element is not {} element)
                {
                    continue;
                }

                associations.spans[SelectorPath.For(element)] = (cell.RowSpan, cell.ColumnSpan);
                cells.Add(element);
            }
        }

        associations.Associate(cells);
        return associations;
    }

    /// <summary>
    /// Resolves each cell's <c>headers</c> attribute against the cells collected above.
    /// </summary>
    /// <remarks>
    /// A reference naming anything other than a cell is dropped. PDF's <c>/Headers</c> holds
    /// structure element ids, so a reference reaching an element with no tag would be a dangling
    /// one — and a document naming a paragraph in its <c>headers</c> attribute has told a browser
    /// nothing either.
    /// </remarks>
    void Associate(List<IElement> cells)
    {
        foreach (var cell in cells)
        {
            if (cell.GetAttribute("headers") is not {Length: > 0} declared)
            {
                continue;
            }

            var resolved = new List<IElement>();

            foreach (var id in declared.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (cell.Owner?.GetElementById(id) is not {} target ||
                    ReferenceEquals(target, cell) ||
                    resolved.Contains(target))
                {
                    continue;
                }

                var path = SelectorPath.For(target);

                if (spans.ContainsKey(path))
                {
                    resolved.Add(target);
                    targets.Add(path);
                }
            }

            if (resolved.Count > 0)
            {
                headers[SelectorPath.For(cell)] = resolved;
            }
        }
    }

    /// <summary>How many rows and columns the cell at <paramref name="path"/> covers, or null.</summary>
    public (int Rows, int Columns)? Spans(string path) =>
        spans.TryGetValue(path, out var span) ? span : null;

    /// <summary>
    /// The header cells the cell at <paramref name="path"/> names, in the order it named them.
    /// </summary>
    public IReadOnlyList<IElement> Headers(string path) =>
        headers.TryGetValue(path, out var found) ? found : [];

    /// <summary>
    /// Whether anything in the document names the cell at <paramref name="path"/> as its header.
    /// </summary>
    /// <remarks>
    /// Only such a cell publishes an <c>/ID</c>. An id nobody references costs a name in the
    /// document's id tree and buys nothing.
    /// </remarks>
    public bool IsHeader(string path) =>
        targets.Contains(path);
}
