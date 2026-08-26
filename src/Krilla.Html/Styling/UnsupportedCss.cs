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
        ("writing-mode", "horizontal-tb", "laid out horizontally"),
        ("direction", "ltr", "laid out left to right"),
        ("column-count", "auto", "laid out in one column")
    ];

    /// <summary>
    /// The two properties that constrain a page break, which are honoured only on request.
    /// </summary>
    /// <remarks>
    /// They are not in the table above because whether they are honoured is a document-wide
    /// decision rather than a property of the value — <see cref="HtmlOptions.HonourOrphansAndWidows"/>
    /// decides, and it is ON by default, so this reports only for a caller who has turned it off.
    /// </remarks>
    static readonly string[] runs = ["orphans", "widows"];

    static readonly string[] corners =
    [
        "border-top-left-radius",
        "border-top-right-radius",
        "border-bottom-right-radius",
        "border-bottom-left-radius"
    ];

    /// <summary>The two properties whose value is a position, read as two components.</summary>
    static readonly string[] positions = ["background-position", "object-position"];

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
        "lower-alpha", "lower-latin", "upper-alpha", "upper-latin", "lower-roman", "upper-roman",
        "lower-greek"
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
        DocumentContext context,
        Action<HtmlDiagnostic> sink)
    {
        var name = element.LocalName;

        if (!context.ConstrainRuns)
        {
            foreach (var property in runs)
            {
                if (Set(declaration, property) is {} value &&
                    value != "2" &&
                    !IsInitial(value))
                {
                    Diagnostic.Property(sink, name, property, value, "pages break between lines");
                }
            }
        }

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

        Hyphens(declaration, name, sink);
        Wrapping(declaration, name, sink);
        Decoration(declaration, name, sink);
        Marker(declaration, name, style, sink);
        Shadows(declaration, name, style, sink);
        Ratio(declaration, name, style, sink);
        Positions(declaration, name, sink);
        Counters(declaration, name, sink);
        Spaces(declaration, name, sink);
        Collapse(declaration, name, sink);
        Outline(declaration, name, sink);
        Background(declaration, name, style, sink);
        Transform(declaration, name, style, sink);
        Casing(declaration, name, sink);
        Fixed(declaration, name, sink);
        Radius(declaration, name, style, sink);
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
        if (Set(declaration, "outline-style") is {} value and not ("none" or "hidden" or "solid") &&
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
        if (Set(declaration, "hyphens") is {} value and ("auto" or "all"))
        {
            Diagnostic.Property(sink, element, "hyphens", value, "words are not hyphenated");
        }
    }

    /// <summary>
    /// A word-breaking value that is recognised and not honoured as asked.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>break-all</c>, <c>break-word</c> and <c>anywhere</c> are implemented and silent, the last
    /// two including the effect on the MINIMUM content width. What is left is <c>keep-all</c>, which
    /// suppresses breaks this engine does not make in the first place — but only for the scripts it
    /// applies to, so it is reported rather than assumed to be a no-op.
    /// </para>
    /// <para>
    /// <c>word-break: break-word</c> is the deprecated spelling of <c>overflow-wrap: break-word</c>
    /// and is not read, so it reports.
    /// </para>
    /// </remarks>
    static void Wrapping(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        if (Set(declaration, "word-break") is {} word and ("keep-all" or "break-word"))
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
    /// An <c>aspect-ratio</c> that named something and resolved to nothing.
    /// </summary>
    /// <remarks>
    /// The two-value <c>auto &lt;ratio&gt;</c> form is what this catches. It applies the ratio only
    /// to a replaced element with no intrinsic dimensions, which is a rule about images that this
    /// engine does not distinguish — so it reads as unparseable and would otherwise be dropped in
    /// silence. A single number is dropped by AngleSharp before it arrives and cannot be reported
    /// at all.
    /// </remarks>
    static void Ratio(
        ICssStyleDeclaration declaration,
        string element,
        ComputedStyle style,
        Action<HtmlDiagnostic> sink)
    {
        if (style.AspectRatio <= 0 &&
            Set(declaration, "aspect-ratio") is {} value &&
            value != "auto" &&
            !IsInitial(value))
        {
            Diagnostic.Property(sink, element, "aspect-ratio", value, "the box is sized by its content");
        }
    }

    /// <summary>
    /// A position given as an edge and an offset, where only two components are read.
    /// </summary>
    /// <remarks>
    /// <c>background-position: right 10px bottom 5px</c> measures each offset from the edge it names
    /// rather than from the start edge, which is four components describing two axes. This engine
    /// reads the first two positionally and would take <c>right</c> and <c>10px</c> as the two axes —
    /// a plausible answer in the wrong place, which is the worst kind. Reported rather than guessed.
    /// </remarks>
    static void Positions(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        foreach (var property in positions)
        {
            if (Set(declaration, property) is not {} value || IsInitial(value))
            {
                continue;
            }

            if (value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 2)
            {
                Diagnostic.Property(sink, element, property, value, "only the first two components are read");
            }
        }
    }

    /// <summary>
    /// A shadow layer this engine cannot draw.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Counted rather than tested value by value, because the resolved list already holds exactly
    /// what will be painted: a declaration naming three layers of which one survived is two layers
    /// short, and the report should say so once per layer lost.
    /// </para>
    /// <para>
    /// What is lost is a blur and a spread. A blur needs a Gaussian, which a PDF content stream
    /// cannot express for an arbitrary shape; a spread is indistinguishable from a blur once
    /// AngleSharp elides the zero between them. <c>inset</c> used to be here and is not — with no
    /// blur it is a subtraction rather than a halo, so it could be drawn exactly.
    /// </para>
    /// </remarks>
    static void Shadows(
        ICssStyleDeclaration declaration,
        string element,
        ComputedStyle style,
        Action<HtmlDiagnostic> sink)
    {
        Report("box-shadow", style.BoxShadows.Length);
        Report("text-shadow", style.TextShadows.Length);

        void Report(string property, int painted)
        {
            if (Set(declaration, property) is not {} value ||
                value == "none" ||
                IsInitial(value))
            {
                return;
            }

            var declared = CssValues.SplitLayers(value).Count;

            if (declared > painted)
            {
                Diagnostic.Property(
                    sink,
                    element,
                    property,
                    value,
                    painted == 0
                        ? "not painted; only an offset with no blur or spread is drawn"
                        : "one or more layers are not painted; only an offset with no blur or spread is drawn");
            }
        }
    }

    /// <summary>
    /// A counter style with no numbering of its own, which is drawn as a disc.
    /// </summary>
    static void Counters(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        if (Set(declaration, "list-style-type") is {} value &&
            !counters.Contains(value) &&
            // A quoted literal is a counter style of its own, and every item shows it.
            value is not (['"', ..] or ['\'', ..]) &&
            !IsInitial(value))
        {
            Diagnostic.Property(sink, element, "list-style-type", value, "the items are marked with a disc");
        }
    }

    /// <summary>
    /// A <c>white-space</c> value the engine does not distinguish, which inherits instead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What is left is <c>break-spaces</c>, which preserves white space and wraps like
    /// <c>pre-wrap</c> and differs from it in one place: a run of trailing spaces may itself be
    /// broken, so a line can end in the middle of one. And the two-value syntax.
    /// </para>
    /// <para>
    /// Which means this cannot currently fire: AngleSharp accepts only the five values the engine
    /// honours and DROPS the rest, so the gap the audit found here is unreachable rather than
    /// unreported. Kept anyway — it costs one lookup and stops the gap reopening if the parser
    /// learns the value, which is the same reason the resolver still maps <c>recto</c>.
    /// </para>
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
    /// <c>position: fixed</c> with no vertical anchor, which is placed once rather than repeated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An anchored fixed box repeats on every page and is not reported. What remains is the box
    /// with <c>top</c> and <c>bottom</c> both auto, which sits at its STATIC position — a position
    /// in the document rather than on a page, so repeating it would add each page's own top to a
    /// coordinate that already includes it. Such a box is painted where flow put it, once.
    /// </para>
    /// <para>
    /// Chromium's printer draws one twice: once where flow put it, straddling a page boundary if
    /// that is where it falls, and again at that page-relative offset on every LATER page. The
    /// divergence is deliberate, so the report is what makes it visible.
    /// </para>
    /// </remarks>
    static void Fixed(ICssStyleDeclaration declaration, string element, Action<HtmlDiagnostic> sink)
    {
        if (Set(declaration, "position") is not "fixed" ||
            Anchored("top") ||
            Anchored("bottom"))
        {
            return;
        }

        Diagnostic.Property(sink, element, "position", "fixed", "placed once rather than repeated on every page");

        bool Anchored(string property) =>
            Set(declaration, property) is {} value && value != "auto";
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
        if (!style.HasBorder)
        {
            return;
        }

        var reason = "the border is painted with square corners";

        if (style.Display == DisplayKind.Inline)
        {
            // An inline element's border is never mitred: a fragment has no corner to mitre at the
            // end where the line broke, so it is a ring where the edges agree on a colour and four
            // rectangles cut to the rounded outline where they do not. What a radius loses there is
            // the INSIDE of the corner rather than the corner itself.
            if (SameColor(style))
            {
                return;
            }

            reason = "an inline element's border is painted with a square inner corner";
        }
        else if (style.PaintsBorderAsRing)
        {
            return;
        }

        foreach (var corner in corners)
        {
            if (Set(declaration, corner) is {} value &&
                !IsZero(value) &&
                !IsInitial(value))
            {
                Diagnostic.Property(sink, element, corner, value, reason);
                return;
            }
        }
    }

    /// <summary>
    /// Whether every border edge with a width agrees on its colour and opacity.
    /// </summary>
    /// <remarks>
    /// The test <see cref="ComputedStyle.PaintsBorderAsRing"/> makes, less the requirement that all
    /// four edges be present and solid. Neither applies to an inline element: a wrapped fragment
    /// has no left or right border at all, and the styles are not drawn there anyway.
    /// </remarks>
    static bool SameColor(ComputedStyle style)
    {
        (float Width, Color? Color, float Alpha)[] sides =
        [
            (style.BorderTop, style.BorderTopColor, style.BorderTopAlpha),
            (style.BorderRight, style.BorderRightColor, style.BorderRightAlpha),
            (style.BorderBottom, style.BorderBottomColor, style.BorderBottomAlpha),
            (style.BorderLeft, style.BorderLeftColor, style.BorderLeftAlpha)
        ];

        Color? found = null;
        var alpha = 1f;

        foreach (var (width, color, sideAlpha) in sides)
        {
            if (width <= 0)
            {
                continue;
            }

            if (color is not {} painted)
            {
                return false;
            }

            if (found is {} already && (already != painted || alpha != sideAlpha))
            {
                return false;
            }

            found = painted;
            alpha = sideAlpha;
        }

        return true;
    }

    /// <summary>
    /// The declared value, lowercased, or null when the cascade carries none.
    /// </summary>
    static string? Set(ICssStyleDeclaration declaration, string property)
    {
        var value = declaration.GetPropertyValue(property);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant();
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
