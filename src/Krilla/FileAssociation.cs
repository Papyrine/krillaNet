namespace Krilla;

/// <summary>
/// How an attachment relates to the document it is embedded in.
/// </summary>
/// <remarks>
/// PDF/A-3 and PDF/A-4f require this to be stated meaningfully.
/// </remarks>
public enum FileAssociation
{
    /// <summary>The source the document was generated from.</summary>
    Source = 0,

    /// <summary>Data the document presents, such as the numbers behind a chart.</summary>
    Data = 1,

    /// <summary>An alternative rendition of the same content.</summary>
    Alternative = 2,

    /// <summary>Supplementary material.</summary>
    Supplement = 3,

    /// <summary>Unstated.</summary>
    Unspecified = 4
}