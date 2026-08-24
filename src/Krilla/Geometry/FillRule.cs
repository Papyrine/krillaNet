namespace Krilla;

/// <summary>
/// How overlapping contours are filled.
/// </summary>
public enum FillRule
{
    /// <summary>The non-zero winding rule.</summary>
    NonZero = 0,

    /// <summary>The even-odd rule.</summary>
    EvenOdd = 1
}