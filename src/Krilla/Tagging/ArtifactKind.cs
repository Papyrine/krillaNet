namespace Krilla;

/// <summary>
/// Content excluded from the logical structure: running heads, page numbers, decorative rules.
/// </summary>
public enum ArtifactKind
{
    /// <summary>A running header.</summary>
    Header = 0,

    /// <summary>A running footer.</summary>
    Footer = 1,

    /// <summary>Background content. Requires a bounding box.</summary>
    Page = 2,

    /// <summary>Anything else.</summary>
    Other = 3
}