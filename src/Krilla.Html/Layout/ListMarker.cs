/// <summary>
/// The marker a list item shows, and where layout put it.
/// </summary>
/// <remarks>
/// Not a <see cref="LayoutBox"/>, deliberately. A marker sits outside its item's principal box, so
/// it contributes nothing to the geometry of anything and does not appear in the browser's
/// <c>getBoundingClientRect()</c> — which means the corpus's box comparison cannot see it, and a
/// marker modelled as a box would show up there as an element the reference does not have. Only
/// the pixel comparison measures a marker.
/// </remarks>
sealed class ListMarker
{
    /// <summary>What this marker shows.</summary>
    public required ListStyleKind Kind { get; init; }

    /// <summary>
    /// This item's number within its list, for the counter styles.
    /// </summary>
    /// <remarks>
    /// Resolved while the tree is built rather than while it is painted, because it depends on
    /// document order among siblings — which the box tree, having dropped every
    /// <c>display: none</c> element and gained anonymous boxes the document never mentioned, is
    /// no longer a faithful record of.
    /// </remarks>
    public required int Ordinal { get; init; }

    /// <summary>
    /// The marker's ink box, in layout units.
    /// </summary>
    /// <remarks>
    /// For a symbol this is the shape's own square. For a counter it is the em box around
    /// <see cref="Run"/>, which is wanted for one thing only: deciding whether the marker falls on
    /// the page being painted.
    /// </remarks>
    public Rect Bounds { get; set; }

    /// <summary>
    /// The positioned glyphs, for a counter style. Null for a symbol, which is drawn as a shape.
    /// </summary>
    public TextRun? Run { get; set; }

    /// <summary>Moves the marker by the given offset.</summary>
    public void Translate(float dx, float dy)
    {
        Bounds = Bounds.Offset(dx, dy);

        if (Run is {} run)
        {
            Run = run with
            {
                X = run.X + dx,
                Y = run.Y + dy
            };
        }
    }
}