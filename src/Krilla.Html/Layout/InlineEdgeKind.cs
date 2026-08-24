/// <summary>Which end of an inline element a marker item stands for.</summary>
enum InlineEdgeKind
{
    /// <summary>Not a marker; ordinary content.</summary>
    None,

    /// <summary>The element's opening edge, carrying its left padding and border.</summary>
    Leading,

    /// <summary>Its closing edge, carrying the right padding and border.</summary>
    Trailing
}