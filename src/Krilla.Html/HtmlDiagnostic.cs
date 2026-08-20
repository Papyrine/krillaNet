namespace Krilla.Html;

/// <summary>
/// Something the converter recognised in the document and did not render the way a browser would.
/// Delivered to <see cref="HtmlOptions.OnDiagnostic"/> as it happens.
/// </summary>
/// <param name="Kind">Whether a CSS property, an HTML attribute, a whole element, or an image.</param>
/// <param name="Element">The element it was found on. For example <c>div</c>.</param>
/// <param name="Name">The property, attribute, or element name. For example <c>float</c>.</param>
/// <param name="Value">The value that was not honoured, when the report turns on one.</param>
/// <param name="Reason">What happened instead. For example <c>laid out in flow</c>.</param>
/// <remarks>
/// The engine implements a subset of CSS and lays out everything else as a plain block, which
/// keeps content on the page but means a document using an unimplemented construct is wrong in a
/// way nothing announces. This is the announcement.
/// </remarks>
public readonly record struct HtmlDiagnostic(
    HtmlDiagnosticKind Kind,
    string Element,
    string Name,
    string? Value,
    string Reason)
{
    /// <summary>
    /// A one-line form, for logging.
    /// </summary>
    public override string ToString()
    {
        var declaration = Value is null ? Name : $"{Name}: {Value}";
        return $"<{Element}> {declaration} — {Reason}";
    }
}
