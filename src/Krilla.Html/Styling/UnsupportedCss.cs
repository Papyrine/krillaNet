/// <summary>
/// Finds declarations the engine reads and does not honour as written, and reports them to
/// <see cref="HtmlOptions.OnDiagnostic"/>.
/// </summary>
/// <remarks>
/// <para>
/// Runs only when a caller has subscribed. It costs a cascade lookup per property per element,
/// which would be a poor trade for a conversion nobody is listening to — so
/// <see cref="DocumentContext.Reports"/> gates it, rather than the sink being checked inside.
/// </para>
/// <para>
/// The table is deliberately a list of properties the engine knows it is getting wrong, not every
/// property AngleSharp hands back. Reporting the whole cascade would bury the signal, and would
/// cost the invariant its meaning: a conversion that reports nothing laid out every construct in
/// the document the way a browser would.
/// </para>
/// <para>
/// A property is reported only when the cascade actually carries it. The cascaded style holds no
/// inherited or initial values, so a property no rule set comes back empty and stays silent — and
/// an author who writes the value that happens to be a no-op here, as a reset stylesheet does with
/// <c>float: none</c>, stays silent too. Either would otherwise report against documents that
/// render correctly.
/// </para>
/// </remarks>
static class UnsupportedCss
{
    /// <summary>
    /// Properties that are not read at all, each with the value that makes ignoring it correct.
    /// </summary>
    static readonly (string Property, string NoOp, string Reason)[] ignored =
    [
        ("box-sizing", "content-box", "sized as content-box, so border and padding add to the width"),
        ("border-collapse", "separate", "laid out with the separated border model"),
        ("overflow", "visible", "not clipped"),
        ("overflow-x", "visible", "not clipped"),
        ("overflow-y", "visible", "not clipped"),
        ("list-style-position", "outside", "the marker is drawn outside the item"),
        ("list-style-image", "none", "the counter style is drawn instead"),
        ("visibility", "visible", "painted anyway"),
        ("opacity", "1", "painted opaque"),
        ("transform", "none", "painted untransformed"),
        ("background-image", "none", "not painted"),
        ("text-transform", "none", "the text is drawn as written"),
        ("letter-spacing", "normal", "the advances of the font are used"),
        ("word-spacing", "normal", "the space advance of the font is used"),
        ("font-variant", "normal", "the regular face is used"),
        ("font-stretch", "normal", "the regular face is used"),
        ("text-shadow", "none", "not painted"),
        ("box-shadow", "none", "not painted"),
        ("caption-side", "top", "the caption is laid out above the grid"),
        ("writing-mode", "horizontal-tb", "laid out horizontally"),
        ("direction", "ltr", "laid out left to right"),
        ("column-count", "auto", "laid out in one column"),
        ("break-before", "auto", "pages break between lines"),
        ("break-after", "auto", "pages break between lines"),
        ("break-inside", "auto", "pages break between lines"),
        ("page-break-before", "auto", "pages break between lines"),
        ("page-break-after", "auto", "pages break between lines"),
        ("page-break-inside", "auto", "pages break between lines"),
        ("orphans", "2", "pages break between lines"),
        ("widows", "2", "pages break between lines")
    ];

    static readonly string[] corners =
    [
        "border-top-left-radius",
        "border-top-right-radius",
        "border-bottom-right-radius",
        "border-bottom-left-radius"
    ];

    static readonly string[] sides = ["top", "right", "bottom", "left"];

    static readonly string[] lines = ["overline", "line-through"];

    static readonly string[] sizes =
    [
        "xx-small", "x-small", "small", "medium", "large", "x-large", "xx-large",
        "smaller", "larger", "initial"
    ];

    /// <summary>
    /// The <c>display</c> values that reach a layout mode of their own. Everything else falls
    /// through to block, which is what gets reported.
    /// </summary>
    static readonly string[] displays =
    [
        "none", "inline", "inline-block", "block", "list-item",
        "table", "inline-table", "table-caption", "table-header-group", "table-row-group",
        "table-footer-group", "table-row", "table-cell", "table-column", "table-column-group"
    ];

    public static void Report(
        IElement element,
        ICssStyleDeclaration declaration,
        ComputedStyle style,
        Action<HtmlDiagnostic> sink)
    {
        var name = element.LocalName;

        Display(declaration, name, sink);

        foreach (var (property, noOp, reason) in ignored)
        {
            if (Set(declaration, property) is {} value &&
                value != noOp &&
                !IsInitial(value))
            {
                Diagnostic.Property(sink, name, property, value, reason);
            }
        }

        Fixed(declaration, name, sink);
        Radius(declaration, name, sink);
        BorderStyles(declaration, name, sink);
        Decoration(declaration, name, sink);
        FontSize(declaration, name, sink);
        CellBaseline(declaration, name, style, sink);
    }

    /// <summary>
    /// A layout mode the engine does not implement, which lays out as a block instead.
    /// </summary>
    static void Display(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        if (Set(declaration, "display") is {} value &&
            !displays.Contains(value))
        {
            Diagnostic.Property(sink, element, "display", value, "laid out as a block");
        }
    }

    /// <summary>
    /// <c>position: fixed</c>, which is placed once rather than repeated.
    /// </summary>
    /// <remarks>
    /// The only positioning value still reported. It is laid out correctly against the page, so
    /// the geometry is right — what is undecided is paged media: CSS says a fixed box repeats on
    /// every page, and this places it on the one page its position falls on. A running header
    /// written that way appears once.
    /// </remarks>
    static void Fixed(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        if (Set(declaration, "position") is "fixed")
        {
            Diagnostic.Property(sink, element, "position", "fixed", "placed once rather than repeated on every page");
        }
    }

    /// <summary>
    /// Rounded corners, which paint square.
    /// </summary>
    /// <remarks>
    /// Read from the four longhands rather than the shorthand, because the cascade expands
    /// <c>border-radius</c> into them, and reading both would report one authored declaration five
    /// times over. Reports once per element for the same reason.
    /// </remarks>
    static void Radius(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        foreach (var corner in corners)
        {
            if (Set(declaration, corner) is {} value &&
                !IsZero(value) &&
                !IsInitial(value))
            {
                Diagnostic.Property(sink, element, corner, value, "painted square");
                return;
            }
        }
    }

    /// <summary>
    /// A border style other than solid, which paints solid.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>none</c> and <c>hidden</c> stay silent because they are honoured exactly:
    /// <see cref="StyleResolver"/> folds them into a zero width, so such a border neither paints
    /// nor takes space.
    /// </para>
    /// <para>
    /// So does <c>hr</c>, whose <c>inset</c> border comes from the default stylesheet rather than
    /// from the document. A plain <c>&lt;hr&gt;</c> in a page carrying no CSS at all would
    /// otherwise report four times, which is the noise this whole table exists to avoid. The
    /// exclusion is by element because the cascade does not expose which origin a declaration came
    /// from — the same limitation that <c>Inputs/flatten.css</c> works around.
    /// </para>
    /// </remarks>
    static void BorderStyles(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        if (element == "hr")
        {
            return;
        }

        var styles = new string?[sides.Length];

        for (var index = 0; index < sides.Length; index++)
        {
            var value = Set(declaration, $"border-{sides[index]}-style");
            styles[index] = value is null or "none" or "hidden" or "solid" || IsInitial(value)
                ? null
                : value;
        }

        // One report for the shorthand the author almost certainly wrote, rather than four for the
        // longhands the cascade expanded it into. Per-side reporting survives for the case that
        // actually differs by side.
        if (styles[0] is {} uniform && styles.All(_ => _ == uniform))
        {
            Diagnostic.Property(sink, element, "border-style", uniform, "painted solid");
            return;
        }

        for (var index = 0; index < sides.Length; index++)
        {
            if (styles[index] is {} value)
            {
                Diagnostic.Property(sink, element, $"border-{sides[index]}-style", value, "painted solid");
            }
        }
    }

    /// <summary>
    /// The decoration lines other than underline, which are not painted.
    /// </summary>
    static void Decoration(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        var value = Set(declaration, "text-decoration-line") ?? Set(declaration, "text-decoration");
        if (value is null)
        {
            return;
        }

        foreach (var line in lines)
        {
            if (value.Contains(line, StringComparison.Ordinal))
            {
                Diagnostic.Property(sink, element, "text-decoration-line", line, "not painted");
            }
        }
    }

    /// <summary>
    /// The font-size keywords, which fall through to the inherited size.
    /// </summary>
    /// <remarks>
    /// <para>
    /// None of them is a length AngleSharp resolves, so every one arrives here as written and none
    /// is honoured. <c>font-size: large</c> is ordinary CSS rather than an exotic case, which is
    /// what makes this worth a report.
    /// </para>
    /// <para>
    /// <c>inherit</c> and <c>unset</c> are absent on purpose: falling through to the inherited size
    /// is exactly what they ask for, so there is nothing to report.
    /// </para>
    /// </remarks>
    static void FontSize(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        if (Set(declaration, "font-size") is {} value &&
            sizes.Contains(value))
        {
            Diagnostic.Property(sink, element, "font-size", value, "the inherited size is used");
        }
    }

    /// <summary>
    /// Baseline alignment of a table cell, which aligns to the top instead.
    /// </summary>
    /// <remarks>
    /// Only reachable by asking for it — the user-agent stylesheet makes a cell <c>middle</c> —
    /// which is why it earns a report rather than being a silent default.
    /// </remarks>
    static void CellBaseline(
        ICssStyleDeclaration declaration,
        string element,
        ComputedStyle style,
        Action<HtmlDiagnostic> sink)
    {
        if (style.Display == DisplayKind.TableCell &&
            Set(declaration, "vertical-align") is "baseline")
        {
            Diagnostic.Property(sink, element, "vertical-align", "baseline", "aligned to the top of the cell");
        }
    }

    /// <summary>
    /// The declared value, lowercased, or null when the cascade carries none.
    /// </summary>
    static string? Set(ICssStyleDeclaration declaration, string property)
    {
        var value = declaration.GetPropertyValue(property);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    static bool IsZero(string value) =>
        value is "0" or "0px" or "0%" or "0em" or "0rem";

    /// <summary>
    /// Whether the value is the keyword meaning the property's own initial value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Silent everywhere in the table above, because for every property listed there the no-op
    /// value IS the initial value — the property is not read at all, which is the same as leaving
    /// it initial. Reporting <c>initial</c> would say a document renders wrongly for asking for the
    /// default.
    /// </para>
    /// <para>
    /// It arrives more often than writing it out would suggest. A shorthand that omits a component
    /// sets that component to <c>initial</c>, so <c>border: 0</c> gives a
    /// <c>border-style: initial</c> the author never typed — which is how this was found, on an
    /// imported test that used it five times.
    /// </para>
    /// <para>
    /// Deliberately not applied to <c>display</c> or <c>font-size</c>: their initial values are
    /// <c>inline</c> and <c>medium</c>, neither of which this engine treats as a no-op.
    /// </para>
    /// </remarks>
    static bool IsInitial(string value) =>
        value == "initial";
}
