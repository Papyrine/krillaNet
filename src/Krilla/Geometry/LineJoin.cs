namespace Krilla;

/// <summary>
/// How corners between stroked segments are drawn.
/// </summary>
public enum LineJoin
{
    /// <summary>Extends the outer edges until they meet.</summary>
    Miter = 0,

    /// <summary>Rounds the corner.</summary>
    Round = 1,

    /// <summary>Cuts the corner off.</summary>
    Bevel = 2
}