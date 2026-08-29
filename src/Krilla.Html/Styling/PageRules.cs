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
/// The geometry is settled once for the document: the size, the orientation and the four margins.
/// The margin BOXES are not, because one may name <c>counter(page)</c> and so has a different
/// answer on every sheet — they are collected here as declared and resolved per page by
/// <see cref="Krilla.Html.Layout.PageMargins"/>, against the pages their selector names.
/// </para>
/// <para>
/// A selector cannot vary the geometry. <c>@page :first { margin-top: 3in }</c> is read for its
/// margin and applied to every page, because a page whose content area differs from the rest is a
/// different layout rather than a different painting, and the document is laid out once.
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
    /// The margin boxes the document declares, in the order they were written.
    /// </summary>
    /// <remarks>
    /// Every one of them, whatever its selector: which apply to a given page is a question only
    /// that page can answer, and there is no page yet when this is read.
    /// </remarks>
    public List<PageMarginRule> MarginBoxes { get; } = [];

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

            // The two things AngleSharp will not give back, recovered from the stylesheet's own
            // source. See `Blocks`.
            foreach (var block in Blocks(sheet))
            {
                var (selector, name) = rules.Selector(block.Selector);

                foreach (var declared in Sizes(block.Body))
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

                foreach (var (slot, declarations) in rules.Margins(block.Body))
                {
                    rules.MarginBoxes.Add(new(selector, rules.MarginBoxes.Count, slot, declarations, name));
                }
            }
        }

        return rules;
    }

    /// <summary>
    /// Every <c>@page</c> rule in a stylesheet's source, as its selector and its body.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AngleSharp.Css parses an <c>@page</c> rule and keeps its margins, and DROPS three things
    /// beside them: the <c>size</c> declaration, the selector, and the margin box at-rules
    /// entirely. <c>Style.GetPropertyValue("size")</c> is empty, the rule's own <c>CssText</c>
    /// comes back without any of it, and there is no object at all for <c>@top-center</c>. There
    /// is nothing to read through the object model, so this reads the text the author wrote.
    /// </para>
    /// <para>
    /// A scan of CSS source is not something to reach for twice, which is why it is one scan
    /// yielding blocks rather than one per thing recovered from them. It is bounded on purpose:
    /// it looks only for <c>@page</c>, matches its braces rather than stopping at the first close,
    /// and hands back the text between them. Anything it cannot read is reported.
    /// </para>
    /// <para>
    /// The one thing it cannot see is the rule tree above it, so an <c>@page</c> nested inside an
    /// <c>@media</c> block is read whatever the query says. A PDF resolves media queries against
    /// PRINT, so the block that matters — <c>@media print</c> — is the one this gets right by
    /// accident; the rest is a known limitation rather than an oversight.
    /// </para>
    /// </remarks>
    static IEnumerable<(string Selector, string Body)> Blocks(ICssStyleSheet sheet)
    {
        if (sheet.OwnerNode is not {} owner)
        {
            yield break;
        }

        // Through the same comment strip `CssSource` uses. This scan counts braces to find a
        // rule's extent, and a comment holding one — `/* } */` — moves the end of every @page
        // block after it.
        var text = CssSource.WithoutComments(owner.TextContent);
        var index = 0;

        while (true)
        {
            index = text.IndexOf("@page", index, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                yield break;
            }

            var open = text.IndexOf('{', index);
            if (open < 0)
            {
                yield break;
            }

            var close = Matching(text, open);
            if (close < 0)
            {
                yield break;
            }

            yield return (text[(index + 5)..open].Trim(), text[(open + 1)..close]);

            index = close;
        }
    }

    /// <summary>The index of the brace closing the one at <paramref name="open"/>, or -1.</summary>
    /// <remarks>
    /// Depth counting rather than a search for the next close, which is the whole of what lets a
    /// margin box be found: a nested at-rule has braces of its own, and the first close belongs to
    /// it rather than to the page.
    /// </remarks>
    static int Matching(string text, int open)
    {
        var depth = 0;

        for (var index = open; index < text.Length; index++)
        {
            if (text[index] == '{')
            {
                depth++;
            }
            else if (text[index] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    /// <summary>
    /// The <c>size</c> declarations in one <c>@page</c> body, in order.
    /// </summary>
    /// <remarks>
    /// Only the body's OWN declarations. Anything inside a nested at-rule belongs to a margin box
    /// and is skipped, which is what stops a <c>size</c> written in one being read as the page's.
    /// </remarks>
    static IEnumerable<string> Sizes(string body)
    {
        foreach (var declaration in Own(body).Split(';', StringSplitOptions.TrimEntries))
        {
            var colon = declaration.IndexOf(':');

            if (colon > 0 &&
                declaration[..colon].Trim().Equals("size", StringComparison.OrdinalIgnoreCase))
            {
                yield return declaration[(colon + 1)..].Trim();
            }
        }
    }

    /// <summary>
    /// The margin box at-rules in one <c>@page</c> body, each with the declarations inside it.
    /// </summary>
    /// <remarks>
    /// A name that is not one of the sixteen is reported rather than skipped. The realistic case
    /// is a spelling mistake, and a running header that silently does not appear is exactly the
    /// kind of absence nobody notices until the document is printed.
    /// </remarks>
    IEnumerable<(PageMarginSlot Slot, string Declarations)> Margins(string body)
    {
        var index = 0;

        while (true)
        {
            index = body.IndexOf('@', index);
            if (index < 0)
            {
                yield break;
            }

            var open = body.IndexOf('{', index);
            if (open < 0)
            {
                yield break;
            }

            var close = Matching(body, open);
            if (close < 0)
            {
                yield break;
            }

            var name = body[(index + 1)..open].Trim();

            if (PageMarginSlots.Parse(name) is {} slot)
            {
                yield return (slot, body[(open + 1)..close]);
            }
            else
            {
                Unsupported.Add((
                    "@page",
                    $"@{name}",
                    "the name is not one of the sixteen page margin boxes, so nothing is drawn for it"));
            }

            index = close;
        }
    }

    /// <summary>
    /// A block's text with every nested at-rule removed, leaving its own declarations.
    /// </summary>
    static string Own(string body)
    {
        if (!body.Contains('@'))
        {
            return body;
        }

        var kept = new StringBuilder();
        var index = 0;

        while (index < body.Length)
        {
            var at = body.IndexOf('@', index);

            if (at < 0)
            {
                kept.Append(body, index, body.Length - index);
                break;
            }

            kept.Append(body, index, at - index);

            var open = body.IndexOf('{', at);
            var close = open < 0 ? -1 : Matching(body, open);

            if (close < 0)
            {
                break;
            }

            index = close + 1;
        }

        return kept.ToString();
    }

    /// <summary>
    /// The pages a selector names, and a report for anything in it that is not read.
    /// </summary>
    /// <remarks>
    /// The four pseudo-classes CSS Paged Media defines, and a NAME. A named page — <c>@page
    /// cover</c> — selects the sheets that the boxes carrying <c>page: cover</c> occupy, which is
    /// why the name has to survive to where the pages are known rather than being resolved here.
    /// </remarks>
    (PageSelector Selector, string? Name) Selector(string text)
    {
        var selector = PageSelector.All;
        var trimmed = text.Trim();

        // Whatever stands before the first colon is the page's name. An empty prefix is the
        // ordinary `@page` and `@page :first` case.
        var colon = trimmed.IndexOf(':');
        var name = (colon < 0 ? trimmed : trimmed[..colon]).Trim();

        foreach (var part in trimmed.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Equals(name, StringComparison.Ordinal))
            {
                continue;
            }

            switch (part.ToLowerInvariant())
            {
                case "first":
                    selector |= PageSelector.First;
                    break;
                case "left":
                    selector |= PageSelector.Left;
                    break;
                case "right":
                    selector |= PageSelector.Right;
                    break;
                case "blank":
                    selector |= PageSelector.Blank;
                    break;
                default:
                    Unsupported.Add((
                        "@page",
                        text,
                        "this page selector is not read, so the rule is dropped rather than applied to every page"));
                    return (PageSelector.Never, null);
            }
        }

        return (selector, name.Length == 0 ? null : name);
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

        // Approximated, and it has to be. `ex` and `ch` name glyph measurements, and which face
        // the root element resolves to is a cascade result — while the cascade cannot be read
        // until the page geometry settled here gives the viewport units something to resolve
        // against. A page sized in `ex` is the price, and nothing writes one.
        var length = CssValues.ParseLength(value, CssFont.Approximate(rootFontSize), root, CssLength.None);

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

            var length = CssValues.ParseLength(
                part,
                // See `Length` above: no face is resolvable this early.
                CssFont.Approximate(rootFontSize),
                root,
                CssLength.None);

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
