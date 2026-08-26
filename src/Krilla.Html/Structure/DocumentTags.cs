namespace Krilla.Html.Structure;

/// <summary>
/// The document's logical structure tree, collected while the pages are painted and built once
/// they have been.
/// </summary>
/// <remarks>
/// <para>
/// A tagged PDF is what makes a document navigable by a screen reader and extractable in reading
/// order, and it is what PDF/UA and PDF/A level A require. HTML carries exactly the semantics a
/// tag tree wants — headings, lists, tables, figures — so the mapping is nearly mechanical; what
/// is not mechanical is the ORDER, which is why this is two passes.
/// </para>
/// <para>
/// Painting cannot produce the tree directly. Content goes down in CSS 2.1 Appendix E's phases
/// rather than in document order, a positioned box is painted from the root rather than where it
/// was declared, and a repeated table header is drawn again on every page — so the sequence a
/// reader would follow is not the sequence the painter emits. Each span of content is therefore
/// recorded against the SELECTOR of the element it came from, and the tree is built afterwards by
/// walking the DOM, which is reading order by construction.
/// </para>
/// <para>
/// What is tagged is the text and the pictures. Everything else is marked as an ARTIFACT rather
/// than left out — a background, a border, an outline, a collapsed table's grid lines, a list
/// marker, a text shadow, a repeated table header and a running margin box — because PDF/UA asks
/// for every operator to be inside one or the other, and content in neither is content a reader
/// may read out at a position nobody chose. krilla's marked content does not nest, which is why
/// the spans are so many and so small: a phase that interleaves text with decoration has to open
/// and close one per piece.
/// </para>
/// </remarks>
sealed class DocumentTags
{
    readonly Dictionary<string, List<Span>> byElement = new(StringComparer.Ordinal);
    int next;

    /// <summary>One recorded span, and where in the painting it was opened.</summary>
    readonly record struct Span(TagIdentifier Identifier, int Order);

    /// <summary>
    /// Notes that <paramref name="identifier"/> is content belonging to
    /// <paramref name="selector"/>.
    /// </summary>
    /// <remarks>
    /// One element usually has several: a paragraph is one span per line, and a heading that
    /// straddles a page break has spans on both sheets.
    /// </remarks>
    public void Record(string selector, TagIdentifier identifier)
    {
        if (!byElement.TryGetValue(selector, out var identifiers))
        {
            byElement[selector] = identifiers = [];
        }

        identifiers.Add(new(identifier, next++));
    }

    /// <summary>Whether anything was recorded at all.</summary>
    public bool IsEmpty => byElement.Count == 0;

    readonly HashSet<object> sighted = [];

    /// <summary>
    /// Whether <paramref name="box"/> is being painted for the FIRST time in this document.
    /// </summary>
    /// <remarks>
    /// Asked by a <c>position: fixed</c> box, which is drawn on every sheet. The repeats are the
    /// same content again and belong in the tree once, so every sighting after this one is painted
    /// as an artifact — the rule a repeated table header follows for the same reason. Reference
    /// identity, since the box is one instance laid out once and drawn per page.
    /// </remarks>
    public bool FirstSighting(object box) =>
        sighted.Add(box);

    /// <summary>
    /// Builds the tree, or returns null when the document produced no tagged content.
    /// </summary>
    /// <remarks>
    /// The caller owns the result and must dispose it after
    /// <see cref="KrillaDocument.SetTagTree"/>.
    /// </remarks>
    public TagTree? Build(IDocument document, string? language)
    {
        if (IsEmpty || document.DocumentElement is not {} root)
        {
            return null;
        }

        var tree = new TagTree();

        try
        {
            tree.WithLanguage(language);

            if (Walk(root) is {} top)
            {
                tree.Add(top);
                return tree;
            }
        }
        catch
        {
            tree.Dispose();
            throw;
        }

        tree.Dispose();
        return null;
    }

    /// <summary>
    /// The tag for <paramref name="element"/> and its subtree, or null when neither it nor
    /// anything under it produced content.
    /// </summary>
    /// <remarks>
    /// An element with nothing under it is skipped rather than tagged empty, which is what keeps
    /// the tree to the elements a reader would meet: a <c>&lt;head&gt;</c>, a wrapper that only
    /// carries a background, and every element a stylesheet hid contribute nothing.
    /// </remarks>
    Tag? Walk(IElement element)
    {
        byElement.TryGetValue(SelectorPath.For(element), out var own);

        List<(Tag Tag, int Order)>? children = null;

        foreach (var child in element.Children)
        {
            if (Walk(child) is {} tag)
            {
                (children ??= []).Add((tag, First(child)));
            }
        }

        if (own is null && children is null)
        {
            return null;
        }

        var self = Create(element);
        var parent = Body(self, element);

        // The children stay in DOM order and the element's OWN spans are merged in among them, by
        // the order the two were PAINTED in. Within CSS 2.1 Appendix E's inline content phase the
        // painter visits lines top to bottom and runs left to right, which is reading order — so a
        // paragraph holding a word in bold produces text, then the bold, then the rest of the text,
        // where before it produced both halves of its own text and then the bold.
        //
        // The merge is deliberately one-sided. Sorting the CHILDREN by paint order too would move a
        // float ahead of the text it was declared after and an absolutely positioned box behind
        // everything, both painting in a phase of their own; keeping them where the document put
        // them means such a child simply takes every own span whose paint order precedes it, which
        // is what the tree did before this.
        var index = 0;

        foreach (var (child, order) in children ?? [])
        {
            while (index < (own?.Count ?? 0) && own![index].Order < order)
            {
                parent.Add(own[index++].Identifier);
            }

            parent.Add(child);
        }

        while (index < (own?.Count ?? 0))
        {
            parent.Add(own![index++].Identifier);
        }

        return self;
    }

    /// <summary>
    /// Where in the painting <paramref name="element"/>'s subtree first put ink, or
    /// <see cref="int.MaxValue"/> when it put none of its own.
    /// </summary>
    int First(IElement element)
    {
        var first = int.MaxValue;

        if (byElement.TryGetValue(SelectorPath.For(element), out var own) && own.Count > 0)
        {
            first = own[0].Order;
        }

        foreach (var child in element.Children)
        {
            first = Math.Min(first, First(child));
        }

        return first;
    }

    /// <summary>
    /// The node an element's content actually hangs from, which for a list item is an
    /// <see cref="TagKind.ListBody"/> nested inside it.
    /// </summary>
    /// <remarks>
    /// PDF's list model is <c>LI</c> holding an optional <c>Lbl</c> — the marker — and an
    /// <c>LBody</c> holding everything else, and a reader announces the two differently. The marker
    /// is painted as an artifact here, so the <c>Lbl</c> is left out rather than made empty; the
    /// <c>LBody</c> is not optional and its absence was the part with nothing to be said for it.
    /// </remarks>
    static Tag Body(Tag tag, IElement element)
    {
        // Reached only for an element that produced content, since a barren one is skipped before
        // this — so the body is never the empty group PDF has no use for.
        if (element.LocalName != "li")
        {
            return tag;
        }

        return tag.Add(TagKind.ListBody);
    }

    /// <summary>
    /// The structural role HTML gives <paramref name="element"/>.
    /// </summary>
    /// <remarks>
    /// The fallback is <see cref="TagKind.Span"/> rather than <see cref="TagKind.Div"/>, because a
    /// reader treats a <c>Div</c> as a grouping and a <c>Span</c> as part of the flow around it —
    /// and the elements that reach the fallback are overwhelmingly inline ones a document invented
    /// for styling.
    /// </remarks>
    static Tag Create(IElement element)
    {
        // An anchor WITH an href is a link and one without is not — HTML's own distinction, and the
        // one a reader acts on. The annotation lands here alongside the text, because `PaintLink`
        // records it against the anchor's own selector rather than the run's.
        if (element.LocalName == "a" && element.GetAttribute("href") is {Length: > 0})
        {
            return Tag.Create(TagKind.Link);
        }

        return element.LocalName switch
        {
            "h1" => Tag.Heading(1, element.TextContent.Trim()),
            "h2" => Tag.Heading(2, element.TextContent.Trim()),
            "h3" => Tag.Heading(3, element.TextContent.Trim()),
            "h4" => Tag.Heading(4, element.TextContent.Trim()),
            "h5" => Tag.Heading(5, element.TextContent.Trim()),
            "h6" => Tag.Heading(6, element.TextContent.Trim()),
            "ol" => Tag.List(Numbering(element)),
            "ul" or "menu" => Tag.List(ListNumbering.Disc),
            "th" => Tag.TableHeader(Scope(element)),
            "img" or "svg" or "figure" => Tag.Figure(Alt(element)),
            "html" => Tag.Create(TagKind.Part),
            "body" or "main" or "article" => Tag.Create(TagKind.Article),
            "section" or "nav" or "aside" or "header" or "footer" => Tag.Create(TagKind.Section),
            "div" or "form" or "fieldset" or "dl" or "dd" or "dt" => Tag.Create(TagKind.Div),
            "p" or "address" or "pre" => Tag.Create(TagKind.Paragraph),
            "blockquote" => Tag.Create(TagKind.BlockQuote),
            "q" => Tag.Create(TagKind.InlineQuote),
            "li" => Tag.Create(TagKind.ListItem),
            "table" => Tag.Create(TagKind.Table),
            "thead" => Tag.Create(TagKind.TableHead),
            "tbody" => Tag.Create(TagKind.TableBody),
            "tfoot" => Tag.Create(TagKind.TableFoot),
            "tr" => Tag.Create(TagKind.TableRow),
            "td" => Tag.Create(TagKind.TableCell),
            "caption" or "figcaption" => Tag.Create(TagKind.Caption),
            "code" or "kbd" or "samp" or "var" => Tag.Create(TagKind.Code),
            "strong" or "b" => Tag.Create(TagKind.Strong),
            "em" or "i" or "cite" or "dfn" => Tag.Create(TagKind.Emphasis),
            "time" => Tag.Create(TagKind.DateTime),
            _ => Tag.Create(TagKind.Span)
        };
    }

    /// <summary>
    /// What a screen reader announces in place of a picture.
    /// </summary>
    /// <remarks>
    /// An <c>&lt;svg&gt;</c> has no <c>alt</c>, so its <c>&lt;title&gt;</c> child is read instead —
    /// which is where SVG itself puts the same information.
    /// </remarks>
    static string? Alt(IElement element)
    {
        if (element.GetAttribute("alt") is {Length: > 0} alt)
        {
            return alt;
        }

        if (element.QuerySelector("title") is {} title && title.TextContent.Trim() is {Length: > 0} text)
        {
            return text;
        }

        return element.GetAttribute("title") is {Length: > 0} attribute ? attribute : null;
    }

    /// <summary>The markers an ordered list shows, as a reader announces them.</summary>
    static ListNumbering Numbering(IElement element) =>
        element.GetAttribute("type") switch
        {
            "a" => ListNumbering.LowerAlpha,
            "A" => ListNumbering.UpperAlpha,
            "i" => ListNumbering.LowerRoman,
            "I" => ListNumbering.UpperRoman,
            _ => ListNumbering.Decimal
        };

    /// <summary>What a header cell describes, from its own <c>scope</c>.</summary>
    static TableHeaderScope Scope(IElement element) =>
        element.GetAttribute("scope")?.Trim().ToLowerInvariant() switch
        {
            "row" or "rowgroup" => TableHeaderScope.Row,
            "col" or "colgroup" => TableHeaderScope.Column,
            _ => TableHeaderScope.Both
        };
}
