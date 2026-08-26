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
                Bounds = Images[index].Bounds.Offset(dx, dy),
                Content = Images[index].Content.Offset(dx, dy)
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