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
    /// The <c>list-style-type</c> values that have a counter style of their own. Everything else
    /// falls through to a disc, which is what gets reported.
    /// </summary>
    /// <remarks>
    /// The same shape as <see cref="displays"/>, and found by the same audit: an unimplemented
    /// counter style still marks its items rather than losing them, on the reasoning that a wrong
    /// marker is visible and a missing one is not — but the marker IS wrong, and nothing said so
    /// until this list existed. <c>lower-greek</c>, <c>armenian</c>, <c>georgian</c> and the CJK
    /// styles all came out as bullets in silence.
    /// </remarks>
    static readonly string[] counters =
    [
        "none", "disc", "circle", "square", "decimal", "decimal-leading-zero",
        "lower-alpha", "lower-latin", "upper-alpha", "upper-latin", "lower-roman", "upper-roman"
    ];

    /// <summary>
    /// The <c>white-space</c> values the engine distinguishes. Everything else inherits, silently,
    /// which is what gets reported.
    /// </summary>
    static readonly string[] whiteSpaces = ["normal", "pre", "pre-wrap", "pre-line", "nowrap"];

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
        Wrapping(declaration, name, sink);
        Tabs(declaration, name, sink);
        Decoration(declaration, name, sink);
        Marker(declaration, name, style, sink);
        Counters(declaration, name, sink);
        Spaces(declaration, name, sink);
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
    /// <para>
    /// Asked of the resolved style rather than of the declaration, so the report follows exactly
    /// what the painter can do: anything <see cref="CssGradient"/> parses or
    /// <see cref="ImageStore"/> resolves is silent, and anything neither of them produces is
    /// reported. That keeps the two from drifting as the accepted syntax grows — a
    /// <c>repeating-</c> gradient, a <c>conic-gradient</c>, a radial one carrying an explicit size,
    /// and a comma-separated stack of layers are all reported today.
    /// </para>
    /// <para>
    /// A <c>url()</c> that resolved to nothing lands here too, which is the right place for it:
    /// from the page's point of view a refused image and an unparseable gradient are the same
    /// absence, and the reason a source was refused has already been decided by the image policy.
    /// </para>
    /// </remarks>
    static void Background(
        ICssStyleDeclaration declaration,
        string element,
        ComputedStyle style,
        Action<HtmlDiagnostic> sink)
    {
        if (style.BackgroundImage is not null || style.BackgroundPicture is not null)
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
    /// Only <c>auto</c> and <c>all</c> report. <c>manual</c> promises breaks at soft hyphens and
    /// nowhere else, which is exactly what this engine does, so it converts as written and stays
    /// silent — and it is the property's initial value, so it arrives far more often than anyone
    /// types it.
    /// </para>
    /// <para>
    /// The gap being reported is a dictionary, not a break rule. Lines break at a hyphen present
    /// in the text and at a soft hyphen the author placed; what is missing is inserting one where
    /// the text offers none.
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
    /// A word-breaking value that is recognised and not honoured as asked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>break-all</c> and <c>break-word</c> are implemented and silent. What is left is
    /// <c>keep-all</c>, which suppresses breaks this engine does not make in the first place — but
    /// only for the scripts it applies to, so it is reported rather than assumed to be a no-op —
    /// and <c>anywhere</c>, which breaks the same way <c>break-word</c> does here and differs in
    /// the one place this does not follow it: it is supposed to narrow the MINIMUM content width
    /// too, so a shrink-to-fit box or a table column holding a long word comes out wider here than
    /// it should.
    /// </para>
    /// <para>
    /// <c>word-break: break-word</c> is the deprecated spelling of <c>overflow-wrap: break-word</c>
    /// and is not read, so it reports.
    /// </para>
    /// </remarks>
    static void Wrapping(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        if (Set(declaration, "word-break") is {} word &&
            word is "keep-all" or "break-word")
        {
            Diagnostic.Property(
                sink,
                element,
                "word-break",
                word,
                word == "keep-all"
                    ? "lines break between words as usual"
                    : "not read; the overflow-wrap spelling is");
        }

        if (Set(declaration, "overflow-wrap") is "anywhere")
        {
            Diagnostic.Property(
                sink,
                element,
                "overflow-wrap",
                "anywhere",
                "lines break inside words, but the minimum content width is not narrowed");
        }
    }

    /// <summary>
    /// A <c>tab-size</c> given as a length, where only a count of space advances is honoured.
    /// </summary>
    /// <remarks>
    /// The two forms need different arithmetic and a field to say which is which, for a value that
    /// has no use in a proportional font and little in a monospaced one. A number — including the
    /// initial 8 — is honoured exactly.
    /// </remarks>
    static void Tabs(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        if (Set(declaration, "tab-size") is {} value &&
            !IsInitial(value) &&
            !CssValues.TryParseNumber(value, out _))
        {
            Diagnostic.Property(sink, element, "tab-size", value, "tab stops are a multiple of the space advance");
        }
    }

    /// <summary>
    /// A <c>list-style-image</c> that did not resolve, where the counter style is drawn instead.
    /// </summary>
    /// <remarks>
    /// Asked of the resolved style, like the background reporters, so an image that loaded is
    /// silent and one the policy refused reports. The fallback is what a browser does, so the
    /// report is about the picture being absent rather than about the marker being wrong — but it
    /// is still worth saying, since a document whose bullets are a brand asset renders with plain
    /// discs and nothing else would say why.
    /// </remarks>
    static void Marker(
        ICssStyleDeclaration declaration,
        string element,
        ComputedStyle style,
        Action<HtmlDiagnostic> sink)
    {
        if (style.MarkerImage is not null)
        {
            return;
        }

        if (Set(declaration, "list-style-image") is {} value &&
            value != "none" &&
            !IsInitial(value))
        {
            Diagnostic.Property(sink, element, "list-style-image", value, "the counter style is drawn instead");
        }
    }

    /// <summary>
    /// A decoration style with no rule this engine can draw.
    /// </summary>
    /// <remarks>
    /// Only <c>wavy</c> is left: solid, double, dashed and dotted are drawn, each measured against
    /// Chrome. A wave needs a path the painter has no shape for, and a solid rule in its place is
    /// the closer of the two answers available — so it is drawn and said to be drawn.
    /// </remarks>
    static void Decoration(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        if (Set(declaration, "text-decoration-style") is "wavy")
        {
            Diagnostic.Property(sink, element, "text-decoration-style", "wavy", "painted as a solid rule");
        }
    }

    /// <summary>
    /// A counter style with no numbering of its own, which is drawn as a disc.
    /// </summary>
    static void Counters(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        if (Set(declaration, "list-style-type") is {} value &&
            !counters.Contains(value) &&
            !IsInitial(value))
        {
            Diagnostic.Property(sink, element, "list-style-type", value, "the items are marked with a disc");
        }
    }

    /// <summary>
    /// A <c>white-space</c> value the engine does not distinguish, which inherits instead.
    /// </summary>
    /// <remarks>
    /// What is left is <c>break-spaces</c>, which preserves white space and wraps like
    /// <c>pre-wrap</c> and differs from it in one place: a run of trailing spaces may itself be
    /// broken, so a line can end in the middle of one. And the two-value syntax, which the cascade
    /// hands back verbatim.
    /// </remarks>
    static void Spaces(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        if (Set(declaration, "white-space") is {} value &&
            !whiteSpaces.Contains(value) &&
            !IsInitial(value))
        {
            Diagnostic.Property(sink, element, "white-space", value, "the inherited handling is used");
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
