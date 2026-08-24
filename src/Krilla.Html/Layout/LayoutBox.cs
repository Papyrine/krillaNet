/// <summary>A rectangle in CSS pixels, with the origin at the top-left of the page.</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
readonly record struct Rect(float X, float Y, float Width, float Height)
{
    /// <summary>Right edge.</summary>
    public float Right => X + Width;

    /// <summary>Bottom edge.</summary>
    public float Bottom => Y + Height;

    /// <summary>This rectangle inset by the given edge widths.</summary>
    public Rect Deflate(float top, float right, float bottom, float left) =>
        new(X + left, Y + top, Math.Max(0, Width - left - right), Math.Max(0, Height - top - bottom));

    /// <summary>This rectangle moved by the given offset.</summary>
    public Rect Offset(float dx, float dy) =>
        new(X + dx, Y + dy, Width, Height);
}

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

/// <summary>
/// A float, and where in its container's flow it was declared.
/// </summary>
/// <param name="Box">The floated box.</param>
/// <param name="Index">
/// The number of in-flow children that precede it. A float is placed at the flow position it was
/// declared at rather than at the top of its container, so a float written after two paragraphs
/// starts below them.
/// </param>
readonly record struct FloatChild(LayoutBox Box, int Index);

/// <summary>Whether an inline image is a list marker, and where it sits.</summary>
enum MarkerPlacement
{
    /// <summary>Not a marker: an ordinary inline image.</summary>
    None,

    /// <summary>
    /// Outside the item's border edge. It takes no advance and is drawn back beyond where the line
    /// starts, while still contributing its height.
    /// </summary>
    Outside,

    /// <summary>
    /// At the start of the item's first line, taking its own width plus the marker gap.
    /// </summary>
    Inside
}

/// <summary>Which end of an inline element a marker item stands for.</summary>
enum InlineEdgeKind
{
    /// <summary>Not a marker; ordinary content.</summary>
    None,

    /// <summary>The element's opening edge, carrying its left padding and border.</summary>
    Leading,

    /// <summary>Its closing edge, carrying the right padding and border.</summary>
    Trailing
}

/// <summary>A run of text waiting to be flowed into lines.</summary>
/// <param name="Text">The text, after white-space processing.</param>
/// <param name="Style">The style it is measured and painted with.</param>
/// <param name="Selector">
/// The selector path of the inline element this run came from, or null when it came straight from
/// a block's own text.
/// </param>
/// <param name="ForcedBreak">
/// Whether this item is a <c>&lt;br&gt;</c> rather than text. Carried as a flag rather than as a
/// newline character because white-space processing would collapse a newline into a space under
/// the default <c>white-space: normal</c>, which is exactly what a forced break must survive.
/// </param>
/// <param name="Image">
/// An image, when this item is an inline-level replaced element rather than text. It occupies a
/// box on the line instead of a run of glyphs, which is what CSS calls an atomic inline.
/// </param>
/// <param name="Link">
/// The <c>href</c> of the enclosing <c>&lt;a&gt;</c>, or null. Carried on the item rather than
/// looked up later because an anchor's text is flattened into the line's runs, so by painting time
/// there is nothing left to ask which element it came from.
/// </param>
/// <param name="Box">
/// A whole box tree, when this item is an <c>inline-block</c>. The other atomic inline: it takes a
/// box on the line the way an image does, and differs in having contents of its own that were laid
/// out in their own formatting context before the line was filled.
/// </param>
/// <param name="Backdrops">
/// The styles of the inline ANCESTORS that paint a background over this item, outermost first.
/// Null when there are none, which is nearly always.
/// </param>
/// <param name="Marker">
/// Set on the image standing in for a list marker. Either way it contributes its height, which is
/// measured: a 32px image marker grows a 24px item to 39px, exactly as an atomic inline of that
/// height would. The two placements differ in the advance they take.
/// </param>
/// <param name="Edge">
/// Set when this item is not content at all but the opening or closing edge of an inline element
/// carrying padding or a border. Emitted only for an element that has one, so a document full of
/// plain <c>&lt;span&gt;</c>s produces none.
/// </param>
sealed record InlineItem(
    string Text,
    ComputedStyle Style,
    string? Selector,
    bool ForcedBreak = false,
    ImageData? Image = null,
    string? Link = null,
    LayoutBox? Box = null,
    IReadOnlyList<ComputedStyle>? Backdrops = null,
    InlineEdgeKind Edge = InlineEdgeKind.None,
    MarkerPlacement Marker = MarkerPlacement.None);

/// <summary>
/// An inline element enclosing a run that is not its own text.
/// </summary>
/// <remarks>
/// An inline element generates no box, so both its painting and its geometry are carried by
/// whatever text sits inside it — including text belonging to a nested inline element, which is
/// the case this exists for. The face is resolved alongside the style because the rectangle is the
/// ANCESTOR's font box rather than the run's: a small nested element inside a large one is backed
/// to the large one's height, and a browser reports it at that height too.
/// </remarks>
readonly record struct InlineBackdrop(ComputedStyle Style, FontFace Face);

/// <summary>
/// The vertical span of an inline element's border box around a baseline.
/// </summary>
/// <remarks>
/// Shared between the painter and the box dump so the rectangle drawn and the rectangle reported
/// cannot drift apart. The font box grown by the element's own padding and border — which
/// OVERFLOWS the line box rather than growing it, CSS's rule for an inline element, and why
/// vertical padding here moves nothing on the page.
/// </remarks>
static class InlineMetrics
{
    public static (float Top, float Bottom) Extent(ComputedStyle style, FontFace face, float baseline)
    {
        var size = style.FontSize;

        return (
            baseline - face.Ascent(size) - style.PaddingTop.Resolve(0) - style.BorderTop,
            baseline + face.Descent(size) + style.PaddingBottom.Resolve(0) + style.BorderBottom);
    }

    /// <summary>The same rectangle horizontally, at <paramref name="style"/>'s own extent.</summary>
    public static Rect Reframe(Rect bounds, ComputedStyle style, FontFace face, float baseline)
    {
        var (top, bottom) = Extent(style, face, baseline);
        return new(bounds.X, top, bounds.Width, bottom - top);
    }
}

/// <summary>One laid-out line, and the glyph runs positioned on it.</summary>
sealed class LineBox
{
    /// <summary>The line's box, spanning the full line height.</summary>
    public Rect Bounds { get; set; }

    /// <summary>Distance from <see cref="Bounds"/>'s top down to the text baseline.</summary>
    public float Baseline { get; set; }

    /// <summary>The runs on this line, left to right.</summary>
    public List<TextRun> Runs { get; } = [];

    /// <summary>The images on this line.</summary>
    public List<InlineImage> Images { get; } = [];

    /// <summary>
    /// The opening and closing edges of the inline elements with a surround on this line.
    /// </summary>
    /// <remarks>
    /// One per element per line at most, and only for an element carrying padding or a border.
    /// They occupy advance on the line like a word does, which is what makes an inline element's
    /// left padding push the text after it along.
    /// </remarks>
    public List<InlineEdgeBox> Edges { get; } = [];

    /// <summary>
    /// The <c>&lt;br&gt;</c> that ended this line, if one did.
    /// </summary>
    /// <remarks>
    /// Carried for the box dump alone: a browser reports a zero-width rectangle for a
    /// <c>&lt;br&gt;</c> at the point the line stopped, and without this it is the last element in
    /// the corpus that the geometry comparison cannot see. Kept apart from
    /// <see cref="Runs"/> rather than added as an empty run, so nothing that paints has to know
    /// about a run with no glyphs in it.
    /// </remarks>
    public List<InlineBreak> Breaks { get; } = [];

    /// <summary>
    /// The <c>inline-block</c> boxes on this line, each already laid out.
    /// </summary>
    /// <remarks>
    /// Held by the line rather than by the containing box's <see cref="LayoutBox.Children"/>,
    /// which keeps the rule that a block container is all-block or all-inline — the same reason
    /// <see cref="LayoutBox.Floats"/> is a list of its own. Everything walking the tree has to
    /// reach them through here: <see cref="LayoutBox.Descendants"/> does, and so do the painter,
    /// the absolute-positioning pass and the box dump.
    /// </remarks>
    public List<LayoutBox> Boxes { get; } = [];

    /// <summary>Moves the line and its contents by the given offset.</summary>
    public void Translate(float dx, float dy)
    {
        Bounds = Bounds.Offset(dx, dy);

        for (var index = 0; index < Runs.Count; index++)
        {
            Runs[index] = Runs[index] with
            {
                X = Runs[index].X + dx,
                Y = Runs[index].Y + dy
            };
        }

        for (var index = 0; index < Images.Count; index++)
        {
            Images[index] = Images[index] with
            {
                Bounds = Images[index].Bounds.Offset(dx, dy)
            };
        }

        for (var index = 0; index < Edges.Count; index++)
        {
            Edges[index] = Edges[index] with
            {
                Bounds = Edges[index].Bounds.Offset(dx, dy),
                Baseline = Edges[index].Baseline + dy
            };
        }

        for (var index = 0; index < Breaks.Count; index++)
        {
            Breaks[index] = Breaks[index] with
            {
                Bounds = Breaks[index].Bounds.Offset(dx, dy)
            };
        }

        foreach (var box in Boxes)
        {
            box.Translate(dx, dy);
        }
    }
}

/// <summary>
/// One end of an inline element on one line: the strip its padding and border occupy.
/// </summary>
/// <param name="Style">The element's style, for the colours and widths.</param>
/// <param name="Face">Its resolved face, which sizes the strip vertically.</param>
/// <param name="Bounds">The strip's border box.</param>
/// <param name="Kind">Which end this is, and so which side border is drawn.</param>
/// <param name="Selector">
/// The element's path. Carried for the box dump, where an edge is what gives a padded inline its
/// real extent — a browser's rectangle for one covers the padding and border, and the text runs
/// alone reach neither.
/// </param>
/// <param name="Baseline">
/// The baseline of the line this edge sits on, so an enclosing element's own extent can be
/// recovered from it.
/// </param>
/// <param name="Ancestors">The inline elements enclosing this one, outermost first, or null.</param>
readonly record struct InlineEdgeBox(
    ComputedStyle Style,
    FontFace Face,
    Rect Bounds,
    InlineEdgeKind Kind,
    string? Selector,
    float Baseline,
    IReadOnlyList<InlineBackdrop>? Ancestors);

/// <summary>A line break element, and the empty rectangle a browser reports for it.</summary>
/// <param name="Selector">The selector path of the <c>&lt;br&gt;</c>.</param>
/// <param name="Bounds">
/// Zero-width, at the end of the line it ended, as tall as the font's ascent plus descent.
/// </param>
readonly record struct InlineBreak(string Selector, Rect Bounds);

/// <summary>An image positioned on a line.</summary>
/// <param name="Image">The image to draw.</param>
/// <param name="Bounds">Where to draw it.</param>
/// <param name="Selector">
/// The selector path of the <c>&lt;img&gt;</c> it came from. Carried so the box dump can report an
/// inline image's geometry: it is a real box whose position and size are known exactly, and
/// omitting it would leave the one replaced element in the corpus unmeasured.
/// </param>
readonly record struct InlineImage(ImageData Image, Rect Bounds, string? Selector);

/// <summary>A positioned run of text, ready to paint.</summary>
/// <param name="Text">The run's text.</param>
/// <param name="Style">The style to paint it with.</param>
/// <param name="Face">The resolved face, already matched against the style.</param>
/// <param name="X">Left edge of the run.</param>
/// <param name="Y">The run's baseline.</param>
/// <param name="Width">The run's advance width.</param>
/// <param name="Link">
/// The <c>href</c> this run links to, or null. One annotation is emitted per run, which is why runs
/// do not merge across a link boundary: a PDF link is a rectangle, so an anchor spanning three
/// lines needs three of them.
/// </param>
/// <param name="Glyphs">
/// The shaped glyphs, already positioned relative to the run's origin. Carried rather than
/// re-derived at paint time so that what is drawn is exactly what the line was measured with —
/// shaping twice would leave the two free to disagree.
/// </param>
/// <param name="Backdrops">
/// The inline ancestors painting a background behind this run, outermost first, or null.
/// </param>
/// <param name="Selector">
/// The path of the element this run's text came from, or null for text with no element of its own.
/// Carried so <see cref="Krilla.Html.Diagnostics.BoxDump"/> can report a rectangle for an inline
/// element, which produces no box and would otherwise be geometry the corpus cannot see at all.
/// </param>
readonly record struct TextRun(
    string Text,
    ComputedStyle Style,
    FontFace Face,
    float X,
    float Y,
    float Width,
    string? Link = null,
    IReadOnlyList<Glyph>? Glyphs = null,
    string? Selector = null,
    IReadOnlyList<InlineBackdrop>? Backdrops = null);
