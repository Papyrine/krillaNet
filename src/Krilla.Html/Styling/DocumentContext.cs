namespace Krilla.Html.Styling;

/// <summary>
/// The state that building a box tree needs once per document rather than once per element: the
/// matched stylesheets, the root font size that <c>rem</c> resolves against, and the resolved
/// images.
/// </summary>
/// <remarks>
/// Exists because reading the cascaded style needs an <see cref="IStyleCollection"/>, and building
/// one costs a pass over every stylesheet in the document.
/// </remarks>
sealed class DocumentContext :
    IDisposable
{
    readonly IStyleCollection styles;
    readonly IDocument document;

    List<((string Prefix, string Pseudo) Selector, DisplayKind Display)>? displays;

    readonly Dictionary<string, List<(string Selector, string Value)>> dropped =
        new(StringComparer.OrdinalIgnoreCase);

    DocumentContext(
        IDocument document,
        IStyleCollection styles,
        CssRoot root,
        ImageStore images,
        FontSet? fonts,
        Action<HtmlDiagnostic>? onDiagnostic)
    {
        this.document = document;
        this.styles = styles;
        Root = root;
        Images = images;
        Fonts = fonts;
        OnDiagnostic = onDiagnostic;
    }

    /// <summary>
    /// The faces the caller supplied, for the two units that need one.
    /// </summary>
    /// <remarks>
    /// Styling has no other use for a font — sizes and families are strings until layout shapes
    /// something. <c>ex</c> and <c>ch</c> are the exception: both name a glyph measurement, so
    /// resolving them means resolving the element's family here, in the same place the cascade is
    /// read. Nullable because a context can be built without one and every unit but those two
    /// still resolves.
    /// </remarks>
    public FontSet? Fonts { get; }

    /// <summary>The root element's font size in CSS pixels.</summary>
    public float RootFontSize => Root.FontSize;

    /// <summary>What <c>rem</c> and the viewport units resolve against.</summary>
    public CssRoot Root { get; }

    /// <summary>
    /// The CSS counters in scope, mutated as the box tree is built.
    /// </summary>
    /// <remarks>
    /// Document-wide state carried here rather than threaded through the builder's recursion,
    /// alongside the images and the cascade — it belongs to the same phase and has the same
    /// lifetime.
    /// </remarks>
    public CssCounters Counters { get; } = new();

    /// <summary>
    /// The column widths collected while one table's children are being walked, and the definitions
    /// they came from.
    /// </summary>
    /// <remarks>
    /// Here rather than on the box builder because the builder is STATIC and conversions run
    /// concurrently — a static field for this raced between two scenarios in the same test run and
    /// produced a table sized by another document's columns. It reads as per-table state and is
    /// per-document state for the same reason the counters are: the walk is where it is filled, and
    /// the walk saves and restores it around each table.
    /// </remarks>
    public List<CssLength> PendingColumns { get; set; } = [];

    /// <inheritdoc cref="PendingColumns"/>
    public List<ColumnBox> PendingColumnBoxes { get; set; } = [];

    /// <summary>
    /// How deeply quotations are nested, for <c>open-quote</c> and <c>close-quote</c>.
    /// </summary>
    /// <remarks>
    /// A single depth for the document rather than one per element, which is what CSS specifies:
    /// the marks a quote draws depend on how many quotations are open anywhere above it, so nesting
    /// a quotation inside another changes its marks.
    /// </remarks>
    public int QuoteDepth { get; set; }

    /// <summary>
    /// Whether <c>orphans</c> and <c>widows</c> are honoured, from
    /// <see cref="HtmlOptions.HonourOrphansAndWidows"/>.
    /// </summary>
    /// <remarks>
    /// Carried here for the diagnostic table alone: the properties are read into every style
    /// regardless, and whether reading them means anything is a document-wide decision the
    /// per-element reporter has no other way to see.
    /// </remarks>
    public bool ConstrainRuns { get; private init; }

    /// <summary>Images resolved from <c>src</c> attributes, deduplicated across the document.</summary>
    public ImageStore Images { get; }

    /// <summary>Where unrendered constructs are reported, or null when nobody subscribed.</summary>
    public Action<HtmlDiagnostic>? OnDiagnostic { get; }

    /// <summary>
    /// Whether anything is listening, so the work of finding what to report is worth doing.
    /// </summary>
    /// <remarks>
    /// Checked before the scan rather than inside it. <see cref="UnsupportedCss"/> costs a cascade
    /// lookup per property per element, which a caller who is not subscribed should not pay for.
    /// </remarks>
    [MemberNotNullWhen(true, nameof(OnDiagnostic))]
    public bool Reports => OnDiagnostic is not null;

    /// <summary>
    /// Builds a context for <paramref name="document"/>.
    /// </summary>
    /// <remarks>
    /// The render device only decides media queries here. Percentages are deliberately not its
    /// business: the cascaded style leaves them unresolved, which is the whole reason for reading
    /// that rather than the computed style.
    /// </remarks>
    public static DocumentContext For(IDocument document, HtmlOptions options)
    {
        var device = new DefaultRenderDevice
        {
            DeviceWidth = (int) options.ContentWidth,
            DeviceHeight = (int) options.ContentHeight,
            ViewPortWidth = (int) options.ContentWidth,
            ViewPortHeight = (int) options.ContentHeight,
            // A PDF is print. Media queries resolved against `Screen` mean a document's
            // `@media print` block — the one written FOR this — was excluded while its
            // `@media screen` block was applied, which is the wrong way round for every document
            // that has both. The corpus reference agrees: its page renders always came from
            // Chromium's printer, and its box geometry now does too.
            Category = DeviceCategory.Printer
        };

        var window = document.DefaultView ??
                     throw new InvalidOperationException("The document has no view to resolve styles against.");

        var images = new ImageStore(
            options.ImageResolver ?? ImageStore.DefaultResolver(options.BaseUrl),
            options.LocalImages,
            options.WebImages,
            options.Fonts);

        return new(
            document,
            window.GetStyleCollection(device),
            // The viewport in paged media is the page's CONTENT box, which is what a browser
            // printing to PDF resolves `vh` and `vw` against — so `height: 100vh` fills the sheet
            // between the margins rather than the sheet itself.
            new(options.RootFontSize, options.ContentWidth, options.ContentHeight),
            images,
            options.Fonts,
            options.OnDiagnostic)
        {
            ConstrainRuns = options.HonourOrphansAndWidows
        };
    }

    /// <summary>
    /// The declarations that matched <paramref name="element"/>, with relative units and
    /// percentages left as written.
    /// </summary>
    /// <remarks>
    /// No inherited values: a property no rule set comes back empty rather than carrying the
    /// parent's. That suits, because inheritance is applied in
    /// <see cref="StyleResolver.Resolve(IElement, ComputedStyle, DocumentContext)"/> against a parent whose values are already resolved —
    /// doing it here would mean inheriting a string and resolving it twice.
    /// </remarks>
    public ICssStyleDeclaration Cascade(IElement element) =>
        styles.ComputeCascadedStyle(element, null!);

    /// <summary>
    /// The cascaded style of one of <paramref name="element"/>'s pseudo-elements, or null when the
    /// document has no rule for it.
    /// </summary>
    /// <remarks>
    /// A separate route from the overload above because a pseudo-element is not an element: it
    /// has no place in the tree and AngleSharp materialises it on request. The style it comes back
    /// with is a real cascade result all the same, carrying whatever the document declared —
    /// including <c>content</c>, which is the one property that only exists here.
    /// </remarks>
    public static ICssStyleDeclaration? Cascade(IElement element, string pseudo) =>
        element.Pseudo(pseudo)?.GetCascadedStyle();

    /// <summary>
    /// The <c>display</c> a <c>::before</c> or <c>::after</c> rule declared for
    /// <paramref name="element"/>, or null when none did.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Recovered from the RULES rather than from the cascade, because the cascade cannot answer it.
    /// A pseudo-element's cascaded style carries the host's declarations too — measured: a
    /// <c>&lt;div&gt;</c> whose <c>::before</c> declares nothing at all comes back with
    /// <c>display: block</c>, leaked from the user-agent rule for <c>div</c>. So a value the two
    /// agree on tells you nothing, and <c>block</c> is exactly the value they agree on for every
    /// block host — which is every host anyone writes a block pseudo for.
    /// </para>
    /// <para>
    /// The same shape as the <c>@page</c> recovery, and bounded the same way: only style rules,
    /// only a selector naming this pseudo, only the <c>display</c> declaration. Cascade order is
    /// approximated by DOCUMENT order — the last matching rule wins and specificity is not compared
    /// — which is a real limitation and a small one, since a document that declares two different
    /// displays for one pseudo-element is a document that has already lost.
    /// </para>
    /// <para>
    /// Media queries are not evaluated here, exactly as the <c>@page</c> scan does not evaluate
    /// them. A PDF resolves media against PRINT, so the block that matters is the one this gets
    /// right by accident.
    /// </para>
    /// </remarks>
    public DisplayKind? PseudoDisplay(IElement element, string pseudo)
    {
        DisplayKind? found = null;

        foreach (var (selector, display) in Displays())
        {
            if (!selector.Pseudo.Equals(pseudo, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Matches(element, selector.Prefix))
            {
                found = display;
            }
        }

        return found;
    }

    /// <summary>
    /// A property the cascade DROPS, taken from the document's own style rules.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AngleSharp parses a declaration it does not recognise into nothing at all, so a property it
    /// has never heard of comes back empty and is indistinguishable from one nobody wrote.
    /// <c>string-set</c> and <c>page</c> are both in that position — the two CSS Paged Media
    /// properties that live on ordinary ELEMENTS rather than inside <c>@page</c>, which is why they
    /// cannot be recovered by the brace scan that rescues the rest of that at-rule.
    /// </para>
    /// <para>
    /// The same limitations the pseudo-element scan carries, and for the same reason: cascade order
    /// is document order, specificity is not compared, media queries are not evaluated, and an
    /// inline <c>style</c> attribute is not seen. A document declaring one property twice for one
    /// element takes the later rule.
    /// </para>
    /// </remarks>
    public string? Declared(IElement element, string property)
    {
        string? found = null;

        foreach (var (selector, value) in Rules(property))
        {
            if (Matches(element, selector))
            {
                found = value;
            }
        }

        return found;
    }

    /// <summary>Every declaration of one property, with the selector that carried it.</summary>
    List<(string Selector, string Value)> Rules(string property)
    {
        if (dropped.TryGetValue(property, out var cached))
        {
            return cached;
        }

        var found = new List<(string, string)>();
        dropped[property] = found;

        foreach (var sheet in document.StyleSheets.OfType<ICssStyleSheet>())
        {
            if (sheet.OwnerNode is not {} owner)
            {
                continue;
            }

            foreach (var (selectors, value) in CssSource.Declarations(owner.TextContent, property))
            {
                foreach (var selector in selectors.Split(','))
                {
                    var text = selector.Trim();

                    // A pseudo-element carries neither of these, and `Matches` throws on the
                    // double-colon form — so a rule naming one is skipped rather than guessed at.
                    if (text.Length > 0 && !text.Contains("::"))
                    {
                        found.Add((text, value));
                    }
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Every <c>display</c> declared on a pseudo-element rule, in document order.
    /// </summary>
    /// <remarks>
    /// Built once and cached, because the scan walks every rule in every stylesheet and a document
    /// with generated content asks this question once per host element.
    /// </remarks>
    List<((string Prefix, string Pseudo) Selector, DisplayKind Display)> Displays()
    {
        if (displays is not null)
        {
            return displays;
        }

        displays = [];

        Walk(rule =>
        {
            var declared = rule.Style.GetPropertyValue("display");

            if (string.IsNullOrWhiteSpace(declared))
            {
                return;
            }

            foreach (var selector in rule.SelectorText.Split(','))
            {
                if (Split(selector) is {} split)
                {
                    displays.Add((split, StyleResolver.PseudoDisplay(declared)));
                }
            }
        });

        return displays;
    }

    /// <summary>
    /// Visits every style rule in the document's own stylesheets, in document order.
    /// </summary>
    /// <remarks>
    /// The DOCUMENT's sheets rather than the matched collection, which does not expose the rules it
    /// matched against — so a grouping rule is descended into without its condition being
    /// evaluated, which is what the callers' remarks about media queries refer to.
    /// </remarks>
    void Walk(Action<ICssStyleRule> visit)
    {
        foreach (var sheet in document.StyleSheets.OfType<ICssStyleSheet>())
        {
            Descend(sheet.Rules);
        }

        void Descend(ICssRuleList rules)
        {
            foreach (var rule in rules)
            {
                if (rule is ICssGroupingRule group)
                {
                    Descend(group.Rules);
                    continue;
                }

                if (rule is ICssStyleRule style)
                {
                    visit(style);
                }
            }
        }
    }

    /// <summary>
    /// A selector's element part and the pseudo-element it names, or null when it names none.
    /// </summary>
    /// <remarks>
    /// Both spellings, since CSS 2.1 wrote one colon and Selectors 3 writes two, and a stylesheet
    /// in the wild carries either. A bare <c>::before</c> with nothing before it matches every
    /// element, which is what the <c>*</c> stands in for.
    /// </remarks>
    static (string Prefix, string Pseudo)? Split(string selector)
    {
        var text = selector.Trim();

        foreach (var name in (string[]) ["before", "after"])
        {
            foreach (var spelling in (string[]) [$"::{name}", $":{name}"])
            {
                if (text.EndsWith(spelling, StringComparison.OrdinalIgnoreCase))
                {
                    var prefix = text[..^spelling.Length].Trim();
                    return (prefix.Length == 0 ? "*" : prefix, name);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Whether <paramref name="element"/> matches a selector, treating one it cannot parse as no
    /// match.
    /// </summary>
    /// <remarks>
    /// A selector reaching here came out of a stylesheet AngleSharp already parsed, so a failure
    /// means a construct its matcher does not implement rather than a syntax error. Swallowing it
    /// leaves the pseudo inline, which is what it was before this existed.
    /// </remarks>
    static bool Matches(IElement element, string selector)
    {
        try
        {
            return element.Matches(selector);
        }
        catch (DomException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public void Dispose() =>
        Images.Dispose();
}
