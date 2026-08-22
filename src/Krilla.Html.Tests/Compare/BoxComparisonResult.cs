/// <summary>
/// How far our geometry is from the browser's, across one scenario.
/// </summary>
public class BoxComparisonResult
{
    /// <summary>Elements found in both trees.</summary>
    public int Matched { get; set; }

    /// <summary>
    /// The largest positional disagreement, in CSS pixels. The single number to watch while
    /// iterating: it goes to zero exactly when the layout is right.
    /// </summary>
    public float WorstOffset { get; set; }

    /// <summary>The largest size disagreement, in CSS pixels.</summary>
    public float WorstSize { get; set; }

    /// <summary>Elements the browser laid out that we did not generate a box for.</summary>
    public List<string> MissingFromRender { get; set; } = [];

    /// <summary>Boxes we generated that the browser has no element for.</summary>
    public List<string> NotInReference { get; set; } = [];

    /// <summary>Every element differing by at least half a pixel, in document order.</summary>
    public List<BoxDiff> Diffs { get; set; } = [];
}