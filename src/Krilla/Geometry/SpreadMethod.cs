namespace Krilla;

/// <summary>
/// How a gradient behaves outside its start and end points.
/// </summary>
public enum SpreadMethod
{
    /// <summary>Extends the terminal colours.</summary>
    Pad = 0,

    /// <summary>Mirrors the gradient repeatedly.</summary>
    Reflect = 1,

    /// <summary>Repeats the gradient from the start.</summary>
    Repeat = 2
}