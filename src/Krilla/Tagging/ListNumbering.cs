namespace Krilla;

/// <summary>
/// How a list's items are numbered or bulleted.
/// </summary>
public enum ListNumbering
{
    /// <summary>No marker.</summary>
    None = 0,

    /// <summary>A filled circle.</summary>
    Disc = 1,

    /// <summary>An open circle.</summary>
    Circle = 2,

    /// <summary>A filled square.</summary>
    Square = 3,

    /// <summary>Arabic numerals.</summary>
    Decimal = 4,

    /// <summary>Lower-case Roman numerals.</summary>
    LowerRoman = 5,

    /// <summary>Upper-case Roman numerals.</summary>
    UpperRoman = 6,

    /// <summary>Lower-case letters.</summary>
    LowerAlpha = 7,

    /// <summary>Upper-case letters.</summary>
    UpperAlpha = 8
}