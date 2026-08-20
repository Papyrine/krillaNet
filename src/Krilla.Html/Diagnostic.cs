// Reports html the converter recognised and then did not render the way a browser would.
//
// Only the deliberate sites report: a declaration that is read and laid out as something else, an
// attribute that is read and not applied, an element that generates no box. Unknown css is left
// alone — reporting every `cursor` and `content` an ordinary stylesheet carries would bury the
// signal under noise, and would cost the invariant its meaning: a conversion that reports nothing
// laid out every construct in the document the way a browser would.
//
// Nothing is built while HtmlOptions.OnDiagnostic is null. The scan that finds these sites is
// itself skipped in that case — see DocumentContext.Reports — because it costs a cascade lookup
// per property per element, which is not a price a caller who is not listening should pay.
static class Diagnostic
{
    internal static void Property(
        Action<HtmlDiagnostic>? sink,
        string element,
        string name,
        string? value,
        string reason) =>
        sink?.Invoke(new(HtmlDiagnosticKind.UnsupportedProperty, element, name, value, reason));

    internal static void Attribute(
        Action<HtmlDiagnostic>? sink,
        string element,
        string name,
        string? value,
        string reason) =>
        sink?.Invoke(new(HtmlDiagnosticKind.IgnoredAttribute, element, name, value, reason));

    internal static void Element(Action<HtmlDiagnostic>? sink, string element, string reason) =>
        sink?.Invoke(new(HtmlDiagnosticKind.UnsupportedElement, element, element, null, reason));

    internal static void Image(Action<HtmlDiagnostic>? sink, string element, string? source, string reason) =>
        sink?.Invoke(new(HtmlDiagnosticKind.UnresolvedImage, element, "src", source, reason));
}
