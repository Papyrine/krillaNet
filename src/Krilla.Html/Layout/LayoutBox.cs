/// <summary>
/// A node in the layout tree: one box, its style, and where layout put it.
/// </summary>
/// <remarks>
/// <para>
/// The tree is built from the DOM but is not the DOM — an element with <c>display: none</c>
/// generates no box, and a block containing both text and blocks generates anonymous boxes the
/// document never mentioned. That divergence is why layout gets its own tree rather than
/// annotating elements.
/// </para>
/// <para>
/// <see cref="BorderBox"/> is the geometry of record, deliberately: it is what a background paints
/// into, what a border strokes around, and — the reason it wins over the content box — what the
/// browser's <c>getBoundingClientRect()</c> returns, so the corpus comparison is comparing like
/// with like.
/// </para>
/// </remarks>
sealed class LayoutBox
{
    /// <summary>The resolved style for this box.</summary>
    /// <remarks>
    /// Settable, and only one caller uses that: <see cref="CollapsedBorders"/> rewrites a table's
    /// boxes to the halved borders the collapsing model gives them, before anything measures one.
    /// Everything else treats it as fixed from construction.
    /// </remarks>
    public required ComputedStyle Style { get; set; }

    /// <summary>
    /// The element this box came from, or null for an anonymous box.
    /// </summary>
    public IElement? Element { get; init; }

    /// <summary>
    /// A selector path identifying <see cref="Element"/>, or null for an anonymous box.
    /// </summary>
    /// <remarks>
    /// The join between our geometry and the browser's. Both sides derive it from the same
    /// document by the same rule (<see cref="SelectorPath"/>), so a box here and a rect from
    /// <c>getBoundingClientRect()</c> can be matched without either side inventing an id.
    /// Anonymous boxes have none, and are excluded from the comparison for that reason.
    /// </remarks>
    public string? Selector { get; init; }

    /// <summary>
    /// Whether this is the root element's box.
    /// </summary>
    /// <remarks>
    /// The root is special in exactly one way that matters here: CSS 2.1 §8.3.1 says its margins
    /// do not collapse. Without that rule the first child's top margin would collapse out through
    /// the root and then be dropped, because the root has no parent to apply it — so every
    /// document whose first element carries a top margin would start flush against the page edge.
    /// </remarks>
    public bool IsRoot { get; init; }

    /// <summary>
    /// The image this box replaces its content with, when it is a block-level replaced element.
    /// </summary>
    /// <remarks>
    /// A replaced box has content that comes from outside CSS, so it sizes from the image's own
    /// dimensions rather than from its children — and it has no children to size from anyway.
    /// </remarks>
    public ImageData? Image { get; init; }

    /// <summary>
    /// The marker this box shows, when it is a list item.
    /// </summary>
    /// <remarks>
    /// Carried on the item rather than modelled as a child box: a marker is placed outside the
    /// principal box, so it neither takes part in layout nor has a counterpart in the browser
    /// geometry the corpus compares against.
    /// </remarks>
    public ListMarker? Marker { get; init; }

    /// <summary>
    /// The grid lines a collapsed table paints, or null for every other box.
    /// </summary>
    /// <remarks>
    /// Held on the table rather than on the cells because a line belongs to the boundary and not to
    /// either box beside it — two cells each painting their half seams at any odd width.
    /// </remarks>
    public List<CollapsedLine>? CollapsedLines { get; set; }

    /// <summary>Child boxes, in document order.</summary>
    public List<LayoutBox> Children { get; } = [];

    /// <summary>
    /// The inline content this box establishes, when it is an inline container.
    /// </summary>
    /// <remarks>
    /// A block container holds either block children or inline content, never a mix — the mixed
    /// case is normalised during box construction by wrapping stray inlines in anonymous blocks.
    /// </remarks>
    public List<InlineItem> Inlines { get; } = [];

    /// <summary>The lines inline layout produced.</summary>
    public List<LineBox> Lines { get; } = [];

    /// <summary>
    /// The floats this box contains, out of flow, each with the position in the flow where it was
    /// declared.
    /// </summary>
    /// <remarks>
    /// Held apart from <see cref="Children"/> rather than mixed in, because a float takes no part
    /// in the flow those describe: it neither advances the stacking position nor contributes to
    /// the height. Keeping it out also preserves the rule that a block container holds either
    /// block children or inline content — a float inside a paragraph would otherwise turn that
    /// paragraph into a mixed container and force an anonymous box around every run of its text.
    /// </remarks>
    public List<FloatChild> Floats { get; } = [];

    /// <summary>
    /// The widths a table's <c>&lt;col&gt;</c> and <c>&lt;colgroup&gt;</c> definitions declare, one
    /// entry per column they cover, in order.
    /// </summary>
    /// <remarks>
    /// A column definition generates no box, so its width has nowhere of its own to live. The list
    /// is positional rather than keyed, which is what lets the grid read the nth column's declared
    /// width without knowing anything about the elements that declared it. Empty for every box that
    /// is not a table with column definitions, which is nearly all of them.
    /// </remarks>
    public List<CssLength> Columns { get; } = [];

    /// <summary>
    /// The <c>&lt;col&gt;</c> and <c>&lt;colgroup&gt;</c> elements a table has, with the columns each
    /// covers and the rectangle it ends up occupying.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Columns"/>, which is one width per column for the sizing algorithm.
    /// This is one entry per ELEMENT, for the box dump: a browser reports a rectangle for a
    /// <c>&lt;col&gt;</c> — its column's extent by the row area's height — and without these the
    /// geometry comparison counts every column definition as an element the engine did not produce.
    /// </remarks>
    public List<ColumnBox> ColumnBoxes { get; } = [];

    /// <summary>
    /// The absolutely positioned boxes this box contains, out of flow.
    /// </summary>
    /// <remarks>
    /// Held against the box they were DECLARED in rather than the one they are positioned against,
    /// which are usually not the same. The declaring parent is what knows their static position —
    /// where flow would have put them, which is where they go when their offsets are auto — and
    /// the containing block is found later, by <see cref="AbsoluteLayout"/> walking the ancestor
    /// chain.
    /// </remarks>
    public List<FloatChild> Positioned { get; } = [];

    /// <summary>
    /// Where flow would have put this box, for an absolutely positioned box whose offsets are
    /// auto.
    /// </summary>
    /// <remarks>
    /// Recorded during normal layout because that is the only moment it exists: the box takes no
    /// space, so nothing afterwards can reconstruct where it would have gone.
    /// </remarks>
    public (float X, float Y)? StaticPosition { get; set; }

    /// <summary>
    /// The border box: the outer edge of the border, which is also the padding box's outer edge
    /// when there is no border.
    /// </summary>
    public Rect BorderBox { get; set; }

    /// <summary>The content box, inset from <see cref="BorderBox"/> by border and padding.</summary>
    public Rect ContentBox { get; set; }

    /// <summary>Whether this box lays out inline content rather than block children.</summary>
    public bool IsInlineContainer => Inlines.Count > 0;

    /// <summary>Every box in this subtree, this one first.</summary>
    /// <remarks>
    /// Floats are included. They are out of FLOW, which is a statement about how they are
    /// positioned, not about whether they exist — everything walking the tree to paint, to
    /// paginate or to compare against a browser needs to see them.
    ///
    /// So are the <c>inline-block</c> boxes held by <see cref="LineBox.Boxes"/>, which are not in
    /// <see cref="Children"/> and would otherwise be invisible to every caller here: the box dump
    /// would report no geometry for one, and a fragment link inside one would resolve to nothing.
    /// </remarks>
    public IEnumerable<LayoutBox> Descendants()
    {
        yield return this;

        foreach (var child in Children)
        {
            foreach (var descendant in child.Descendants())
            {
                yield return descendant;
            }
        }

        foreach (var line in Lines)
        {
            foreach (var atomic in line.Boxes)
            {
                foreach (var descendant in atomic.Descendants())
                {
                    yield return descendant;
                }
            }
        }

        foreach (var floated in Floats)
        {
            foreach (var descendant in floated.Box.Descendants())
            {
                yield return descendant;
            }
        }

        foreach (var positioned in Positioned)
        {
            foreach (var descendant in positioned.Box.Descendants())
            {
                yield return descendant;
            }
        }
    }

    /// <summary>Moves this box and everything under it by the given offset.</summary>
    public void Translate(float dx, float dy)
    {
        BorderBox = BorderBox.Offset(dx, dy);
        ContentBox = ContentBox.Offset(dx, dy);

        Marker?.Translate(dx, dy);

        foreach (var line in Lines)
        {
            line.Translate(dx, dy);
        }

        foreach (var child in Children)
        {
            child.Translate(dx, dy);
        }

        // Floats move with their container even though they were positioned absolutely. A table
        // cell lays its contents out at a provisional origin and is moved once its row height is
        // known, and a float left behind would part company with the lines that were shortened
        // around it.
        foreach (var floated in Floats)
        {
            floated.Box.Translate(dx, dy);
        }

        // Moves with the box that declared it, which keeps a recorded static position meaningful
        // while flow is still settling. Absolute layout runs afterwards and positions against a
        // containing block, so anything it has already placed is unaffected by construction.
        foreach (var positioned in Positioned)
        {
            positioned.Box.Translate(dx, dy);
        }
    }
}