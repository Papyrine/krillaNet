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