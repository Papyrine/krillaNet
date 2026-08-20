namespace Krilla.Html;

/// <summary>
/// What a <see cref="HtmlDiagnostic"/> is reporting.
/// </summary>
public enum HtmlDiagnosticKind
{
    /// <summary>
    /// A CSS declaration that was read and not honoured as written. The box is laid out or painted
    /// as something else, which <see cref="HtmlDiagnostic.Reason"/> names.
    /// </summary>
    UnsupportedProperty,

    /// <summary>
    /// An HTML attribute that was read and not applied.
    /// </summary>
    IgnoredAttribute,

    /// <summary>
    /// An element that a browser lays out and this generates no box for, so it contributed
    /// nothing beyond its children.
    /// </summary>
    UnsupportedElement,

    /// <summary>
    /// An <c>&lt;img&gt;</c> whose <c>src</c> produced no image, so nothing was drawn for it.
    /// </summary>
    UnresolvedImage
}
