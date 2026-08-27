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
/// What is tagged is the text and the pictures, plus a counter list marker, which is the one piece
/// of generated decoration a reader needs told. Everything else is marked as an ARTIFACT rather
/// than left out — a background, a border, an outline, a collapsed table's grid lines, a symbol
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
    readonly Dictionary<string, List<Span>> byMarker = new(StringComparer.Ordinal);
    readonly Dictionary<string, Tag> cells = new(StringComparer.Ordinal);
    TableAssociations? associations;
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
    public void Record(string selector, TagIdentifier identifier) =>
        Record(byElement, selector, identifier);

    /// <summary>
    /// Notes that <paramref name="identifier"/> is the MARKER of the list item at
    /// <paramref name="selector"/>.
    /// </summary>
    /// <remarks>
    /// Kept apart from the item's own content because PDF keeps the two apart: an <c>LI</c> holds
    /// an optional <c>Lbl</c> for the marker and an <c>LBody</c> for everything else, and a reader
    /// announces them differently. The marker is painted at the END of block layout, from outside
    /// the walk that puts the item's own text down, so the split costs nothing but a second
    /// dictionary.
    /// </remarks>
    public void RecordMarker(string selector, TagIdentifier identifier) =>
        Record(byMarker, selector, identifier);

    void Record(Dictionary<string, List<Span>> into, string selector, TagIdentifier identifier)
    {
        if (!into.TryGetValue(selector, out var identifiers))
        {
            into[selector] = identifiers = [];
        }

        identifiers.Add(new(identifier, next++));
    }

    /// <summary>Whether anything was recorded at all.</summary>
    public bool IsEmpty => byElement.Count == 0 && byMarker.Count == 0;

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
    /// <param name="document">The document, walked in reading order.</param>
    /// <param name="language">The document's language, as a BCP 47 tag.</param>
    /// <param name="root">
    /// The laid-out root box, from which a table cell's resolved spans are read.
    /// </param>
    /// <remarks>
    /// The caller owns the result and must dispose it after
    /// <see cref="KrillaDocument.SetTagTree"/>.
    /// </remarks>
    public TagTree? Build(IDocument document, string? language, LayoutBox root)
    {
        if (IsEmpty || document.DocumentElement is not {} element)
        {
            return null;
        }

        associations = TableAssociations.Build(root);

        var tree = new TagTree();

        try
        {
            tree.WithLanguage(language);

            if (Walk(element) is {} top)
            {
                // After the walk, because a `headers` attribute may name a cell anywhere in the
                // table — including one the walk has not reached yet — and a reference to a cell
                // that produced no content at all is a dangling one. Both questions are answered
                // by what the walk actually built, so they can only be asked once it has.
                Link();

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
        var path = SelectorPath.For(element);

        byElement.TryGetValue(path, out var own);
        byMarker.TryGetValue(path, out var marker);

        List<(Tag Tag, int Order)>? children = null;

        foreach (var child in element.Children)
        {
            if (Walk(child) is {} tag)
            {
                (children ??= []).Add((tag, First(child)));
            }
        }

        if (own is null && marker is null && children is null)
        {
            return null;
        }

        var node = Create(element, path);
        var parent = Body(node, marker, own is not null || children is not null);

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

        return node.Tag;
    }

    /// <summary>
    /// Where in the painting <paramref name="element"/>'s subtree first put ink, or
    /// <see cref="int.MaxValue"/> when it put none of its own.
    /// </summary>
    int First(IElement element)
    {
        var first = int.MaxValue;
        var path = SelectorPath.For(element);

        // The marker as well as the text, because it is drawn AHEAD of the item's own lines — so
        // an item whose text is all in child elements still first put ink where its bullet went.
        if (byMarker.TryGetValue(path, out var marker) && marker.Count > 0)
        {
            first = marker[0].Order;
        }

        if (byElement.TryGetValue(path, out var own) && own.Count > 0)
        {
            first = Math.Min(first, own[0].Order);
        }

        foreach (var child in element.Children)
        {
            first = Math.Min(first, First(child));
        }

        return first;
    }

    /// <summary>
    /// Associates each cell with the header cells its <c>headers</c> attribute names.
    /// </summary>
    /// <remarks>
    /// A reference to a cell the walk skipped is dropped rather than written. PDF resolves
    /// <c>/Headers</c> through the document's id tree, and an id nothing published is a reference
    /// to nowhere — worse than the association being absent, because a reader following it lands
    /// on nothing rather than falling back to the cell's own column.
    /// </remarks>
    void Link()
    {
        foreach (var (path, tag) in cells)
        {
            if (associations!.Headers(path) is not {Count: > 0} named)
            {
                continue;
            }

            var ids = new List<string>();

            foreach (var target in named)
            {
                if (cells.ContainsKey(SelectorPath.For(target)) && target.Id is {Length: > 0} id)
                {
                    ids.Add(id);
                }
            }

            if (ids.Count > 0)
            {
                tag.WithHeaders(ids);
            }
        }
    }

    /// <summary>
    /// The node an element's content actually hangs from, which for a list item is an
    /// <see cref="TagKind.ListBody"/> nested inside it.
    /// </summary>
    /// <remarks>
    /// PDF's list model is <c>LI</c> holding an optional <c>Lbl</c> — the marker — and an
    /// <c>LBody</c> holding everything else, and a reader announces the two differently. The
    /// <c>Lbl</c> is present exactly when the marker put glyphs on the page, which is the counter
    /// styles: a disc, a circle and a square are drawn as shapes and stay artifacts, since a reader
    /// announcing a bullet before every item would be announcing what the list's own tag says.
    /// </remarks>
    static Tag Body(TagNode node, List<Span>? marker, bool content)
    {
        // A `Lbl` outside an `LI` is not a structure PDF has, so an item whose `role` took its
        // item-ness away keeps the marker's glyphs as ordinary content of whatever it did become.
        // Dropping them instead would leave marked content on the page that no structure element
        // references, which is the one thing tagging is meant to make impossible.
        if (!node.Item)
        {
            foreach (var span in marker ?? [])
            {
                node.Tag.Add(span.Identifier);
            }

            return node.Tag;
        }

        if (marker is not null)
        {
            var label = node.Tag.Add(TagKind.ListLabel);

            foreach (var span in marker)
            {
                label.Add(span.Identifier);
            }
        }

        // An item with a marker and nothing else is the one arrangement that reaches here with no
        // content — a barren element is skipped before this, and a marker is not barren — so the
        // `LBody` is left off rather than written empty, which is a group PDF has no use for.
        return content ? node.Tag.Add(TagKind.ListBody) : node.Tag;
    }

    /// <summary>
    /// The structure element <paramref name="element"/> produces, described and associated.
    /// </summary>
    TagNode Create(IElement element, string path)
    {
        var node = AriaSemantics.Role(element) ?? Native(element);

        Describe(node.Tag, element);

        if (node.Cell)
        {
            Associate(node.Tag, element, path);
        }

        return node;
    }

    /// <summary>
    /// What a reader is told about the content beyond its role.
    /// </summary>
    /// <remarks>
    /// <c>/Alt</c> is the only field a PDF reader is given for either a name or a description, so
    /// an element carrying both puts them there together — a description that reached nowhere would
    /// be an <c>aria-describedby</c> the conversion silently dropped. A picture already took its
    /// name at construction, and re-setting the same string is what makes that harmless.
    /// </remarks>
    static void Describe(Tag tag, IElement element)
    {
        var name = AriaSemantics.Name(element);
        var description = AriaSemantics.Description(element);

        if (description is not null)
        {
            tag.WithAltText(name is null ? description : $"{name}. {description}");
        }
        else if (name is not null)
        {
            tag.WithAltText(name);
        }

        if (AriaSemantics.Expansion(element) is {} expanded)
        {
            tag.WithExpanded(expanded);
        }
    }

    /// <summary>
    /// Gives a table cell the shape it occupies and the id anything referencing it will use.
    /// </summary>
    /// <remarks>
    /// A span of one is left unwritten: it is the default, so writing it says nothing and costs an
    /// entry in every cell of every table.
    /// </remarks>
    void Associate(Tag tag, IElement element, string path)
    {
        if (associations!.Spans(path) is {} span)
        {
            if (span.Rows > 1)
            {
                tag.WithRowSpan(span.Rows);
            }

            if (span.Columns > 1)
            {
                tag.WithColumnSpan(span.Columns);
            }
        }

        if (associations.IsHeader(path) && element.Id is {Length: > 0} id)
        {
            tag.WithId(id);
        }

        cells[path] = tag;
    }

    /// <summary>
    /// The structural role HTML itself gives <paramref name="element"/>.
    /// </summary>
    /// <remarks>
    /// The fallback is <see cref="TagKind.Span"/> rather than <see cref="TagKind.Div"/>, because a
    /// reader treats a <c>Div</c> as a grouping and a <c>Span</c> as part of the flow around it —
    /// and the elements that reach the fallback are overwhelmingly inline ones a document invented
    /// for styling.
    /// </remarks>
    static TagNode Native(IElement element)
    {
        // An anchor WITH an href is a link and one without is not — HTML's own distinction, and the
        // one a reader acts on. The annotation lands here alongside the text, because `PaintLink`
        // records it against the anchor's own selector rather than the run's.
        if (element.LocalName == "a" && element.GetAttribute("href") is {Length: > 0})
        {
            return new(Tag.Create(TagKind.Link));
        }

        return element.LocalName switch
        {
            "h1" => new(Tag.Heading(1, element.TextContent.Trim())),
            "h2" => new(Tag.Heading(2, element.TextContent.Trim())),
            "h3" => new(Tag.Heading(3, element.TextContent.Trim())),
            "h4" => new(Tag.Heading(4, element.TextContent.Trim())),
            "h5" => new(Tag.Heading(5, element.TextContent.Trim())),
            "h6" => new(Tag.Heading(6, element.TextContent.Trim())),
            "ol" => new(Tag.List(Numbering(element))),
            "ul" or "menu" => new(Tag.List(ListNumbering.Disc)),
            "th" => new(Tag.TableHeader(Scope(element)), Cell: true),
            "img" or "svg" or "figure" => new(Tag.Figure(AriaSemantics.Picture(element))),
            "html" => new(Tag.Create(TagKind.Part)),
            "body" or "main" or "article" => new(Tag.Create(TagKind.Article)),
            "section" or "nav" or "aside" or "header" or "footer" => new(Tag.Create(TagKind.Section)),
            "div" or "form" or "fieldset" or "dl" or "dd" or "dt" => new(Tag.Create(TagKind.Div)),
            "p" or "address" or "pre" => new(Tag.Create(TagKind.Paragraph)),
            "blockquote" => new(Tag.Create(TagKind.BlockQuote)),
            "q" => new(Tag.Create(TagKind.InlineQuote)),
            "li" => new(Tag.Create(TagKind.ListItem), Item: true),
            "table" => new(Summarised(element)),
            "thead" => new(Tag.Create(TagKind.TableHead)),
            "tbody" => new(Tag.Create(TagKind.TableBody)),
            "tfoot" => new(Tag.Create(TagKind.TableFoot)),
            "tr" => new(Tag.Create(TagKind.TableRow)),
            "td" => new(Tag.Create(TagKind.TableCell), Cell: true),
            "caption" or "figcaption" => new(Tag.Create(TagKind.Caption)),
            "code" or "kbd" or "samp" or "var" => new(Tag.Create(TagKind.Code)),
            "strong" or "b" => new(Tag.Create(TagKind.Strong)),
            "em" or "i" or "cite" or "dfn" => new(Tag.Create(TagKind.Emphasis)),
            "time" => new(Tag.Create(TagKind.DateTime)),
            _ => new(Tag.Create(TagKind.Span))
        };
    }

    /// <summary>A table, carrying the prose description of its own shape if it has one.</summary>
    /// <remarks>
    /// <c>&lt;table summary&gt;</c> is obsolete in HTML5 and is exactly what PDF's <c>/Summary</c>
    /// is for — a sentence saying how the grid is arranged, for a reader that cannot see it. The
    /// obsolescence is not a reason to drop it: the documents this converter is pointed at come
    /// disproportionately from reporting tools, which are also the last things still emitting it.
    /// </remarks>
    static Tag Summarised(IElement element)
    {
        var tag = Tag.Create(TagKind.Table);

        if (element.GetAttribute("summary") is {Length: > 0} summary)
        {
            tag.WithSummary(summary);
        }

        return tag;
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
