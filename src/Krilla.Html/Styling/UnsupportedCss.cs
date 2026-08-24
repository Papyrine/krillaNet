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
        ("list-style-image", "none", "the counter style is drawn instead"),
        ("font-variant", "normal", "the regular face is used"),
        ("font-stretch", "normal", "the regular face is used"),
        ("text-shadow", "none", "not painted"),
        ("box-shadow", "none", "not painted"),
        ("writing-mode", "horizontal-tb", "laid out horizontally"),
        ("direction", "ltr", "laid out left to right"),
        ("column-count", "auto", "laid out in one column"),
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

    /// <summary>
    /// The two break properties whose values are only partly honoured, in the modern spelling.
    /// </summary>
    /// <remarks>
    /// <c>break-inside</c> is absent because the only value it takes that means anything outside a
    /// multi-column or paged-region context is <c>avoid</c>, and that one is honoured — the box
    /// becomes an unbreakable unit. Its other values behave as <c>auto</c> here by the
    /// specification's own rule rather than by omission, so reporting them would be a false
    /// positive.
    /// </remarks>
    static readonly string[] breakEdges = ["break-before", "break-after"];

    /// <summary>
    /// The <c>text-transform</c> values that are applied. Everything else falls through to the
    /// inherited casing, which is what gets reported.
    /// </summary>
    /// <remarks>
    /// The two absent from this list are <c>full-width</c> and <c>full-size-kana</c>, which map
    /// characters onto different ones rather than re-casing them. Neither is a casing operation
    /// <see cref="TextTransform"/> could reach by upper-casing, and both are silent about failing.
    /// </remarks>
    static readonly string[] transforms = ["none", "uppercase", "lowercase", "capitalize"];

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

        Breaks(declaration, name, sink);
        Hyphens(declaration, name, sink);
        Collapse(declaration, name, sink);
        Outline(declaration, name, sink);
        HiddenEdge(declaration, name, style, sink);
        Background(declaration, name, style, sink);
        Transform(declaration, name, style, sink);
        Casing(declaration, name, sink);
        InlineSurround(declaration, name, style, sink);
        Fixed(declaration, name, sink);
        Radius(declaration, name, style, sink);
        BorderStyles(declaration, name, sink);
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
    /// A break request that is recognised and not honoured as asked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The forced values are honoured and silent, which is what this whole method is arranged
    /// around: after the pagination work, most of what an author writes here is rendered the way a
    /// browser renders it, and blanket-reporting the property would now be reporting documents
    /// that convert correctly. What is left is two cases.
    /// </para>
    /// <para>
    /// <c>avoid</c> asks for a break to be MOVED rather than taken, which the slice cannot do at a
    /// box edge: <see cref="Paginator"/> chooses where a page ends from the unbreakable units
    /// below it, and has no notion of a candidate it should reject in favour of an earlier one.
    /// <c>break-inside: avoid</c> is different, and honoured, because it names a rectangle to keep
    /// together rather than an edge to keep clear.
    /// </para>
    /// <para>
    /// The four that name a side get their break taken and the side ignored. That is a partial
    /// answer rather than a wrong one, and it still earns a report: a browser inserts a further
    /// blank page to land on the sheet asked for, so a document using them has a different page
    /// COUNT here, not merely different page furniture.
    /// </para>
    /// </remarks>
    static void Breaks(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        foreach (var edge in breakEdges)
        {
            var (property, value) = Declared(declaration, edge);

            var reason = value switch
            {
                "avoid" or "avoid-page" => "a page break is taken where pagination puts it",
                "left" or "right" or "recto" or "verso" =>
                    "a page is started, but not necessarily on that side of the sheet",
                _ => null
            };

            if (reason is not null)
            {
                Diagnostic.Property(sink, element, property, value!, reason);
            }
        }
    }

    /// <summary>
    /// <c>border-style: hidden</c> inside a collapsed table, which does not suppress its
    /// neighbour's border.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one thing the collapsing model does that this engine cannot. CSS gives <c>hidden</c>
    /// absolute priority over every other border at a shared edge — it wins even against a wider
    /// one — which is how a table hides an internal rule without touching the cells around it.
    /// </para>
    /// <para>
    /// It cannot be honoured because <see cref="StyleResolver"/> folds <c>none</c> and
    /// <c>hidden</c> into a zero width before anything downstream sees them, so by the time the
    /// edges are resolved a hidden border is indistinguishable from an absent one and simply loses
    /// on width. Silent everywhere else, because outside a collapsed table the two really are the
    /// same thing.
    /// </para>
    /// </remarks>
    static void HiddenEdge(
        ICssStyleDeclaration declaration,
        string element,
        ComputedStyle style,
        Action<HtmlDiagnostic> sink)
    {
        if (style.BorderCollapse != BorderCollapseKind.Collapse)
        {
            return;
        }

        foreach (var side in sides)
        {
            if (Set(declaration, $"border-{side}-style") is "hidden")
            {
                Diagnostic.Property(
                    sink,
                    element,
                    "border-style",
                    "hidden",
                    "the neighbouring border is drawn anyway");
                return;
            }
        }
    }

    /// <summary>
    /// An outline style other than solid, which is not drawn at all.
    /// </summary>
    /// <remarks>
    /// Not "painted solid", which is what a border of an unsupported style gets: an outline is
    /// decoration with no layout consequence, so drawing the wrong one is a worse answer than
    /// drawing none. <see cref="StyleResolver"/> zeroes the width to match, which is why the
    /// report is the only trace such an outline leaves.
    /// </remarks>
    static void Outline(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        if (Set(declaration, "outline-style") is {} value &&
            value is not ("none" or "hidden" or "solid") &&
            !IsInitial(value))
        {
            Diagnostic.Property(sink, element, "outline-style", value, "not painted");
        }
    }

    /// <summary>
    /// A <c>transform</c> carrying a function this engine does not apply.
    /// </summary>
    /// <remarks>
    /// Asked of the resolved style, so the report follows exactly what the painter can do. What is
    /// left is the three-dimensional functions and <c>perspective</c>: applying their
    /// two-dimensional shadow would put the box somewhere plausible and wrong, so the whole
    /// transform is dropped and said to be dropped.
    /// </remarks>
    static void Transform(
        ICssStyleDeclaration declaration,
        string element,
        ComputedStyle style,
        Action<HtmlDiagnostic> sink)
    {
        if (style.Transform is not null)
        {
            return;
        }

        if (Set(declaration, "transform") is {} value &&
            value != "none" &&
            !IsInitial(value))
        {
            Diagnostic.Property(sink, element, "transform", value, "painted untransformed");
        }
    }

    /// <summary>
    /// A <c>background-image</c> that is not a gradient this engine draws.
    /// </summary>
    /// <remarks>
    /// Asked of the resolved style rather than of the declaration, so the report follows exactly
    /// what the painter can do: anything <see cref="CssGradient"/> parses is silent and anything
    /// it returns null for is reported. That keeps the two from drifting as the accepted syntax
    /// grows — a `url()`, a `repeating-` gradient, a `conic-gradient`, a radial one carrying an
    /// explicit size, and a comma-separated stack of layers are all reported today.
    /// </remarks>
    static void Background(
        ICssStyleDeclaration declaration,
        string element,
        ComputedStyle style,
        Action<HtmlDiagnostic> sink)
    {
        if (style.BackgroundImage is not null)
        {
            return;
        }

        if (Set(declaration, "background-image") is {} value &&
            value != "none" &&
            !IsInitial(value))
        {
            Diagnostic.Property(sink, element, "background-image", value, "not painted");
        }
    }

    /// <summary>
    /// <c>visibility: collapse</c>, which hides rather than removing the track.
    /// </summary>
    /// <remarks>
    /// <c>hidden</c> is honoured and silent. <c>collapse</c> differs from it on a table row or
    /// column alone, where it removes the track and lets the rows below move up rather than
    /// leaving a blank band — so the report is about the space, not about the ink. Everywhere else
    /// the two are the same thing and this over-reports slightly, which is the right way round:
    /// the alternative is knowing the element's display before the style is resolved.
    /// </remarks>
    static void Collapse(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        if (Set(declaration, "visibility") is "collapse")
        {
            Diagnostic.Property(
                sink,
                element,
                "visibility",
                "collapse",
                "hidden, and still occupying its space");
        }
    }

    /// <summary>
    /// A <c>text-transform</c> that is not a casing operation.
    /// </summary>
    static void Casing(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        if (Set(declaration, "text-transform") is {} value &&
            !transforms.Contains(value) &&
            !IsInitial(value))
        {
            Diagnostic.Property(sink, element, "text-transform", value, "the text is drawn as written");
        }
    }

    /// <summary>
    /// The parts of an inline element's box that are still not drawn.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Most of the inline box model IS honoured — the background, the padding, the border and the
    /// horizontal margins, each per line fragment — so what is left is two decorations that a real
    /// box gets and a fragment does not.
    /// </para>
    /// <para>
    /// A rounded corner cannot be reported through <see cref="Radius"/>, which asks whether the
    /// border is painted as one ring and answers yes for the uniform solid case. An inline border
    /// is never a ring: it is up to four rectangles, because a fragment has no corners to mitre at
    /// the end where the line broke.
    /// </para>
    /// <para>
    /// Vertical margins are silent, and correctly so: CSS drops them on an inline element, so
    /// ignoring them is what a browser does rather than something left undone.
    /// </para>
    /// </remarks>
    static void InlineSurround(
        ICssStyleDeclaration declaration,
        string element,
        ComputedStyle style,
        Action<HtmlDiagnostic> sink)
    {
        if (style.Display != DisplayKind.Inline)
        {
            return;
        }

        if (style.BackgroundImage is not null)
        {
            Diagnostic.Property(
                sink,
                element,
                "background-image",
                Set(declaration, "background-image") ?? "set",
                "only the background colour is painted on an inline element");
        }

        foreach (var corner in corners)
        {
            if (Set(declaration, corner) is {} value &&
                !IsZero(value) &&
                !IsInitial(value))
            {
                Diagnostic.Property(
                    sink,
                    element,
                    corner,
                    value,
                    "an inline element's border is painted with square corners");
                return;
            }
        }
    }

    /// <summary>
    /// Automatic hyphenation, which is not performed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only <c>auto</c> and <c>all</c> report. <c>manual</c> is what an author writes to DISABLE
    /// automatic hyphenation, and a document asking for what this engine already does converts
    /// exactly as written — reporting it would be a false positive of precisely the kind the table
    /// exists to avoid, and it is also the property's initial value, so it arrives more often than
    /// anyone types it.
    /// </para>
    /// <para>
    /// The gap being reported is a dictionary, not a break rule. Lines DO break at a hyphen that
    /// is present in the text; what is missing is inserting one where the text has none.
    /// <c>manual</c> promises breaks at soft hyphens, which are not implemented either — so it is
    /// silent here despite a difference that a document containing <c>&amp;shy;</c> would show.
    /// That is a deliberate trade of one rare false negative against a common false positive.
    /// </para>
    /// </remarks>
    static void Hyphens(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        if (Set(declaration, "hyphens") is {} value &&
            value is "auto" or "all")
        {
            Diagnostic.Property(sink, element, "hyphens", value, "words are not hyphenated");
        }
    }

    /// <summary>
    /// A break property's declared value, under whichever of its two spellings carries it.
    /// </summary>
    /// <remarks>
    /// The modern spelling is preferred and the legacy one is the fallback, matching what
    /// <see cref="StyleResolver"/> resolves from — a report naming a property the resolver did not
    /// read would point at the wrong declaration. The name comes back alongside the value so the
    /// report names the spelling the AUTHOR used, which is the one they can search their
    /// stylesheet for.
    /// </remarks>
    static (string Property, string? Value) Declared(ICssStyleDeclaration declaration, string property)
    {
        if (Set(declaration, property) is {} modern)
        {
            return (property, modern);
        }

        var legacy = $"page-{property}";
        return (legacy, Set(declaration, legacy));
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
    static void Radius(
        ICssStyleDeclaration declaration,
        string element,
        ComputedStyle style,
        Action<HtmlDiagnostic> sink)
    {
        // Honoured for the background always, and for a border only where the border is painted as
        // one ring — which needs every edge solid and every edge the same colour. Anything else
        // falls back to four mitred trapezia, which have square corners, so the radius is honoured
        // on the fill underneath and lost on the frame over it.
        if (!style.HasBorder || style.PaintsBorderAsRing)
        {
            return;
        }

        foreach (var corner in corners)
        {
            if (Set(declaration, corner) is {} value &&
                !IsZero(value) &&
                !IsInitial(value))
            {
                Diagnostic.Property(
                    sink,
                    element,
                    corner,
                    value,
                    "the border is painted with square corners");
                return;
            }
        }
    }

    /// <summary>
    /// A border style that is drawn as solid because it needs shading.
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
            styles[index] = value is null or "none" or "hidden" or "solid"
                                     or "dashed" or "dotted" or "double" ||
                            IsInitial(value)
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
    /// Whether the value is one of the keywords meaning "leave this property alone".
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
    /// <c>unset</c> is the same answer by a different route. It means <c>inherit</c> for an
    /// inherited property and <c>initial</c> for the rest, and every property in the table above is
    /// one the engine ignores entirely — so where it inherits, whatever the ancestor declared was
    /// already reported on the ancestor, and where it does not, this is <c>initial</c> exactly.
    /// Either way the declaration adds no difference of its own to report.
    /// </para>
    /// <para>
    /// <c>revert</c> is absent because it never arrives: AngleSharp drops the declaration
    /// carrying it rather than passing the keyword through, so the cascaded style comes back empty
    /// and this is never asked. Listing it would suggest a case that had been handled when it is
    /// one that cannot be seen.
    /// </para>
    /// <para>
    /// Deliberately not applied to <c>display</c> or <c>font-size</c>: their initial values are
    /// <c>inline</c> and <c>medium</c>, neither of which this engine treats as a no-op.
    /// </para>
    /// </remarks>
    static bool IsInitial(string value) =>
        value is "initial" or "unset";
}
