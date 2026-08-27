namespace Krilla.Html.Structure;

/// <summary>
/// A structure element, with the two things about it the tree still has to ask.
/// </summary>
/// <param name="Tag">The tag itself.</param>
/// <param name="Cell">
/// Whether it is a table cell, and so may carry a span and a header association. Asked rather than
/// inferred from the element's name, because <c>role</c> decides it: a <c>&lt;td&gt;</c> marked
/// <c>role="presentation"</c> is not a cell, and krilla refuses a span on anything that is not one.
/// </param>
/// <param name="Item">
/// Whether it is a list item, whose content hangs from an <c>LBody</c> beside the <c>Lbl</c> its
/// marker produces. Asked for the same reason.
/// </param>
readonly record struct TagNode(Tag Tag, bool Cell = false, bool Item = false);
