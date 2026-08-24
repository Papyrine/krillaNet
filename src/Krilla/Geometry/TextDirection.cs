namespace Krilla;

/// <summary>
/// The reading direction assumed when shaping a line of text.
/// </summary>
public enum TextDirection
{
    /// <summary>Inferred from the text.</summary>
    Auto = 0,

    /// <summary>Left to right.</summary>
    LeftToRight = 1,

    /// <summary>Right to left.</summary>
    RightToLeft = 2
}