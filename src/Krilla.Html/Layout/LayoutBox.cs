namespace Krilla.Html.Layout;

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
    public required ComputedStyle Style { get; init; }

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
    /// The border box: the outer edge of the border, which is also the padding box's outer edge
    /// when there is no border.
    /// </summary>
    public Rect BorderBox { get; set; }

    /// <summary>The content box, inset from <see cref="BorderBox"/> by border and padding.</summary>
    public Rect ContentBox { get; set; }

    /// <summary>Whether this box lays out inline content rather than block children.</summary>
    public bool IsInlineContainer => Inlines.Count > 0;

    /// <summary>Every box in this subtree, this one first.</summary>
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
    }

    /// <summary>Moves this box and everything under it by the given offset.</summary>
    public void Translate(float dx, float dy)
    {
        BorderBox = BorderBox.Offset(dx, dy);
        ContentBox = ContentBox.Offset(dx, dy);

        foreach (var line in Lines)
        {
            line.Translate(dx, dy);
        }

        foreach (var child in Children)
        {
            child.Translate(dx, dy);
        }
    }
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
sealed record InlineItem(
    string Text,
    ComputedStyle Style,
    string? Selector,
    bool ForcedBreak = false,
    ImageData? Image = null);

/// <summary>One laid-out line, and the glyph runs positioned on it.</summary>
sealed class LineBox
{
    /// <summary>The line's box, spanning the full line height.</summary>
    public Rect Bounds { get; set; }

    /// <summary>Distance from <see cref="Bounds"/>'s top down to the text baseline.</summary>
    public float Baseline { get; set; }

    /// <summary>The runs on this line, left to right.</summary>
    public List<TextRun> Runs { get; } = [];

    /// <summary>The atomic inlines on this line — images, currently.</summary>
    public List<InlineImage> Images { get; } = [];

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
    }
}

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
readonly record struct TextRun(
    string Text,
    ComputedStyle Style,
    FontFace Face,
    float X,
    float Y,
    float Width);
