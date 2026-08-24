namespace Krilla.Html.Styling;

/// <summary>
/// The page geometry a document's <c>@page</c> rules ask for.
/// </summary>
/// <remarks>
/// <para>
/// A document that declares its own page size means it, and a converter that ignores the
/// declaration prints an A4 report onto US Letter — a whole-document difference with no diagnostic
/// to explain it. So <c>@page</c> wins over <see cref="HtmlOptions"/> by default, and
/// <see cref="HtmlOptions.HonourPageRules"/> is how a caller who has already decided the paper
/// keeps it.
/// </para>
/// <para>
/// Only the geometry is read. The margin BOXES — <c>@top-center</c> and the fifteen others that
/// carry running headers — are a layout mode of their own and are reported rather than
/// approximated, as is the page selector: <c>@page :first</c> and <c>:left</c>/<c>:right</c> would
/// need a per-page cascade, where everything here is settled once for the document.
/// </para>
/// </remarks>
sealed class PageRules
{
    /// <summary>The named sizes CSS Paged Media defines, in CSS pixels, portrait.</summary>
    /// <remarks>
    /// From the specification's own table rather than measured, since a paper size is a definition
    /// rather than a rendering. The millimetre sizes are converted at 96 pixels per inch, which is
    /// what makes <c>A4</c> come out at 793.7 by 1122.5 rather than at a round number.
    /// </remarks>
    static readonly Dictionary<string, (float Width, float Height)> named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["a5"] = Millimetres(148, 210),
        ["a4"] = Millimetres(210, 297),
        ["a3"] = Millimetres(297, 420),
        ["b5"] = Millimetres(176, 250),
        ["b4"] = Millimetres(250, 353),
        ["jis-b5"] = Millimetres(182, 257),
        ["jis-b4"] = Millimetres(257, 364),
        ["letter"] = Inches(8.5f, 11),
        ["legal"] = Inches(8.5f, 14),
        ["ledger"] = Inches(11, 17)
    };

    /// <summary>The page's width and height in CSS pixels, or null where nothing was declared.</summary>
    public (float Width, float Height)? Size { get; private set; }

    /// <summary>
    /// Whether an orientation keyword was declared, and which.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Size"/> because <c>size: landscape</c> alone turns whatever paper
    /// the caller chose, naming none of its own. Applied by the caller, which is the only place
    /// both halves are known.
    /// </remarks>
    public bool? Landscape { get; private set; }

    /// <summary>The four margins in CSS pixels, each null where nothing was declared.</summary>
    public float? MarginTop { get; private set; }

    /// <summary>The right margin.</summary>
    public float? MarginRight { get; private set; }

    /// <summary>The bottom margin.</summary>
    public float? MarginBottom { get; private set; }

    /// <summary>The left margin.</summary>
    public float? MarginLeft { get; private set; }

    /// <summary>
    /// The parts of the <c>@page</c> rules that were recognised and not honoured.
    /// </summary>
    /// <remarks>
    /// Collected rather than reported directly, because <c>@page</c> is read once for the document
    /// while <see cref="HtmlDiagnostic"/> is delivered per element — so the caller hands these to
    /// the sink itself, with no element to attribute them to.
    /// </remarks>
    public List<(string Property, string Value, string Reason)> Unsupported { get; } = [];

    /// <summary>Whether anything was found at all.</summary>
    public bool Any =>
        Size is not null ||
        Landscape is not null ||
        MarginTop is not null ||
        MarginRight is not null ||
        MarginBottom is not null ||
        MarginLeft is not null;

    /// <summary>
    /// Reads every <c>@page</c> rule in <paramref name="document"/>, later rules winning.
    /// </summary>
    /// <remarks>
    /// Later wins rather than a real cascade, because <c>@page</c> has no specificity to compare —
    /// every rule here selects the same page box, the selectors that would distinguish them being
    /// unreadable through AngleSharp.
    /// </remarks>
    public static PageRules For(IDocument document, float rootFontSize)
    {
        var rules = new PageRules();
        var root = new CssRoot(rootFontSize, 0, 0);

        foreach (var sheet in document.StyleSheets.OfType<ICssStyleSheet>())
        {
            foreach (var rule in sheet.Rules.OfType<ICssPageRule>())
            {
                rules.Read(rule, root, rootFontSize);
            }

            // The declaration AngleSharp will not give back, recovered from the stylesheet's own
            // source. See `Sizes`.
            foreach (var declared in rules.Sizes(sheet))
            {
                var (paper, landscape) = Dimensions(declared, root, rootFontSize);

                if (paper is null && landscape is null)
                {
                    rules.Unsupported.Add(("size", declared, "the page size named here is not read"));
                    continue;
                }

                rules.Size = paper ?? rules.Size;
                rules.Landscape = landscape ?? rules.Landscape;
            }
        }

        return rules;
    }

    /// <summary>
    /// The <c>size</c> declarations in a stylesheet's source, in order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AngleSharp.Css parses an <c>@page</c> rule and keeps its margins, and DROPS <c>size</c>
    /// entirely — the rule's own <c>CssText</c> comes back without it and
    /// <c>Style.GetPropertyValue("size")</c> is empty. There is nothing to read through the object
    /// model, so this reads the text the author wrote.
    /// </para>
    /// <para>
    /// A scan of CSS source is not something to reach for twice, and it is bounded here on purpose:
    /// it looks only inside <c>@page</c> blocks, takes only a <c>size</c> declaration, and stops at
    /// the first closing brace — so a nested block or a comment carrying the word makes it find
    /// nothing rather than find something wrong. Anything it cannot read is reported.
    /// </para>
    /// </remarks>
    IEnumerable<string> Sizes(ICssStyleSheet sheet)
    {
        if (sheet.OwnerNode is not {} owner)
        {
            yield break;
        }

        var text = owner.TextContent;
        var index = 0;

        while (true)
        {
            index = text.IndexOf("@page", index, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                yield break;
            }

            var open = text.IndexOf('{', index);
            var close = open < 0 ? -1 : text.IndexOf('}', open);
            index += 5;

            if (open < 0 || close < 0)
            {
                yield break;
            }

            // Whatever stood between `@page` and its brace is a page selector, which would need a
            // per-page cascade: everything here is settled once for the document.
            var selector = text[(index)..open].Trim();

            if (selector.Length > 0)
            {
                Unsupported.Add((
                    "@page",
                    selector,
                    "the rule applies to every page rather than to the ones the selector names"));
            }

            var block = text[(open + 1)..close];

            // A margin box is a nested at-rule, and the scan stopped at the first closing brace —
            // so its declarations are not in `block` and the `size` inside one is not read either.
            // Reported rather than skipped silently, since a running header is the reason most
            // documents have an `@page` rule at all.
            if (block.Contains('@'))
            {
                Unsupported.Add((
                    "@page",
                    block[block.IndexOf('@')..].Split(' ', '{')[0],
                    "page margin boxes are not laid out, so running headers and footers are absent"));
            }

            foreach (var declaration in block.Split(';', StringSplitOptions.TrimEntries))
            {
                var colon = declaration.IndexOf(':');

                if (colon > 0 &&
                    declaration[..colon].Trim().Equals("size", StringComparison.OrdinalIgnoreCase))
                {
                    yield return declaration[(colon + 1)..].Trim();
                }
            }

            index = close;
        }
    }

    void Read(ICssPageRule rule, CssRoot root, float rootFontSize)
    {
        MarginTop = Length(rule, "margin-top", root, rootFontSize) ?? MarginTop;
        MarginRight = Length(rule, "margin-right", root, rootFontSize) ?? MarginRight;
        MarginBottom = Length(rule, "margin-bottom", root, rootFontSize) ?? MarginBottom;
        MarginLeft = Length(rule, "margin-left", root, rootFontSize) ?? MarginLeft;
    }

    static float? Length(ICssPageRule rule, string property, CssRoot root, float rootFontSize)
    {
        var value = rule.Style.GetPropertyValue(property);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var length = CssValues.ParseLength(value, rootFontSize, root, CssLength.None);

        // A percentage margin resolves against the page's own dimension, which is not settled until
        // the size below is — and a page margin given as a percentage is rare enough that taking
        // the fallback is a better trade than ordering the two.
        if (length.Kind == LengthKind.Absolute)
        {
            return length.Value;
        }

        return null;
    }

    /// <summary>
    /// A <c>size</c> value as a width and height, or null when it names neither.
    /// </summary>
    /// <remarks>
    /// The grammar is a named size, one or two lengths, and either orientation keyword, in any
    /// order — so the parts are classified rather than read positionally. Two lengths are a width
    /// and a height in that order and an orientation keyword does not apply to them, which is the
    /// specification's rule and keeps <c>size: 20cm 10cm landscape</c> from transposing a page the
    /// author already transposed.
    /// </remarks>
    static ((float Width, float Height)? Paper, bool? Landscape) Dimensions(
        string value,
        CssRoot root,
        float rootFontSize)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        (float Width, float Height)? paper = null;
        var lengths = new List<float>();
        var landscape = false;
        var oriented = false;

        foreach (var part in parts)
        {
            if (part.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (part.Equals("landscape", StringComparison.OrdinalIgnoreCase))
            {
                landscape = true;
                oriented = true;
                continue;
            }

            if (part.Equals("portrait", StringComparison.OrdinalIgnoreCase))
            {
                oriented = true;
                continue;
            }

            if (named.TryGetValue(part, out var found))
            {
                paper = found;
                continue;
            }

            var length = CssValues.ParseLength(part, rootFontSize, root, CssLength.None);

            if (length.Kind != LengthKind.Absolute || length.Value <= 0)
            {
                return (null, null);
            }

            lengths.Add(length.Value);
        }

        // Two lengths are a width and a height in that order, and an orientation keyword does not
        // apply to them — the specification's rule, and what keeps `size: 20cm 10cm landscape` from
        // transposing a page the author already transposed.
        if (lengths.Count == 2)
        {
            return ((lengths[0], lengths[1]), null);
        }

        if (lengths.Count == 1)
        {
            // One length is a SQUARE page, which is what the specification says and not what an
            // author writing `size: 20cm` for a width expects.
            return ((lengths[0], lengths[0]), null);
        }

        // With nothing but an orientation keyword the paper stays whatever the caller chose, and
        // only the turn is ours to apply.
        return (paper, oriented ? landscape : null);
    }

    static (float Width, float Height) Millimetres(float width, float height) =>
        (width * 96 / 25.4f, height * 96 / 25.4f);

    static (float Width, float Height) Inches(float width, float height) =>
        (width * 96, height * 96);
}
