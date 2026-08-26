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
    readonly Dictionary<string, List<TagIdentifier>> byElement = new(StringComparer.Ordinal);

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

        identifiers.Add(identifier);
    }

    /// <summary>Whether anything was recorded at all.</summary>
    public bool IsEmpty => byElement.Count == 0;

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

        List<Tag>? children = null;

        foreach (var child in element.Children)
        {
            if (Walk(child) is {} tag)
            {
                (children ??= []).Add(tag);
            }
        }

        if (own is null && children is null)
        {
            return null;
        }

        var parent = Create(element);

        // The element's own content first, then its children's. Where an element has both — a
        // paragraph holding a word in bold — that is not quite reading order, since its own text
        // is on both sides of the child. Recorded in todo.md: putting them in order needs the
        // spans to carry a position in the source, which a selector path does not.
        foreach (var identifier in own ?? [])
        {
            parent.Add(identifier);
        }

        foreach (var child in children ?? [])
        {
            parent.Add(child);
        }

        return parent;
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
    static Tag Create(IElement element) =>
        element.LocalName switch
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
            _ => Tag.Create(TagKind.Span)
        };

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
