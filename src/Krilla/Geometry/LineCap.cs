namespace Krilla;

/// <summary>
/// How the ends of an open stroked contour are drawn.
/// </summary>
public enum LineCap
{
    /// <summary>Ends exactly at the endpoint.</summary>
    Butt = 0,

    /// <summary>Extends by a semicircle.</summary>
    Round = 1,

    /// <summary>Extends by half the stroke width.</summary>
    Square = 2
}