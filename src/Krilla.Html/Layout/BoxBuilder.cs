/// <summary>
/// Builds the layout tree from the DOM.
/// </summary>
/// <remarks>
/// Three things happen here that make the result differ from the document: elements with
/// <c>display: none</c> generate nothing, text is put through white-space processing, and a block
/// that would otherwise hold both blocks and inline content has its inline content wrapped in
/// anonymous blocks. The last is what lets <see cref="BlockLayout"/> assume a block container is
/// either all-block or all-inline, which is an assumption worth a lot of simplicity downstream.
/// </remarks>
static class BoxBuilder
{
    /// <summary>
    /// Builds the tree rooted at <paramref name="root"/>.
    /// </summary>
    public static LayoutBox Build(IElement root, ComputedStyle initial, DocumentContext context)
    {
        var style = StyleResolver.Resolve(root, initial, context);
        var box = new LayoutBox
        {
            Style = style,
            Element = root,
            Selector = SelectorPath.For(root),
            IsRoot = true
        };

        AddChildren(box, root, style, context, link: null);
        return box;
    }

    static void AddChildren(
        LayoutBox box,
        IElement element,
        ComputedStyle style,
        DocumentContext context,
        string? link)
    {
        var blocks = new List<LayoutBox>();
        var inlines = new List<InlineItem>();
        var floats = new List<FloatChild>();
        var positioned = new List<FloatChild>();

        // One counter per element, whether or not it turns out to hold list items. It is the only
        // thing that knows how many items have been generated so far, which is what makes a
        // `display: none` item skip a number rather than consume one.
        var numbering = ListNumbering.For(element);

        // The CSS counters this element resets and increments, in that order — CSS's own, and
        // observable: resetting and incrementing the same counter here gives 1 rather than 0, which
        // is how a numbered heading restarts its own subsection numbering. Applied before the
        // ::before content is generated, so `counter()` there reads the incremented value.
        var pushed = context.Counters.Enter(style);

        // A table inside a cell has its own column definitions, so the outer table's are set aside
        // for the duration rather than shared — the alternative is one table's <col> widths sizing
        // another's columns.
        var outerColumns = context.PendingColumns;
        var outerColumnBoxes = context.PendingColumnBoxes;
        context.PendingColumns = [];
        context.PendingColumnBoxes = [];

        Generated(element, "before", style, context, inlines, link, blocks);

        foreach (var node in element.ChildNodes)
        {
            Collect(node, style, context, blocks, inlines, floats, positioned, link, numbering);
        }

        Generated(element, "after", style, context, inlines, link, blocks);

        // The scopes this element created end with its subtree, which is what keeps a second list
        // from continuing the first one's numbering.
        context.Counters.Leave(pushed);

        if (context.PendingColumns.Count > 0 && style.Display == DisplayKind.Table)
        {
            box.Columns.AddRange(context.PendingColumns);
            box.ColumnBoxes.AddRange(context.PendingColumnBoxes);
        }

        context.PendingColumns = outerColumns;
        context.PendingColumnBoxes = outerColumnBoxes;

        // A marker IMAGE is inline content rather than a marker drawn beside the item, which is
        // what makes it grow the item's first line: a 32px image takes a 24px item to 39px, exactly
        // as an atomic inline of that height does. Prepended here, before the runs are closed, so
        // it lands on the item's own first line.
        //
        // Only when the item has inline content of its own. An item whose whole content is a block
        // has no line here to hang it from, and `Marker` has already given it a counter marker to
        // fall back to.
        if (box.Marker is null &&
            style is {Display: DisplayKind.ListItem, MarkerImage: { } marker} &&
            inlines.Count > 0)
        {
            inlines.Insert(
                0,
                new(
                    "",
                    style,
                    null,
                    Image: marker,
                    Marker: style.ListStylePosition == ListStylePositionKind.Inside
                        ? MarkerPlacement.Inside
                        : MarkerPlacement.Outside));
        }

        // A run with no block after it never met a boundary to be closed at. Only when a block
        // turned up at all: with none, the container is all-inline and the runs stay runs.
        if (blocks.Count > 0)
        {
            CloseRun(blocks, inlines, style);
        }

        box.Floats.AddRange(floats);
        box.Positioned.AddRange(positioned);

        // A block container is either all-block or all-inline. When both turned up, the runs
        // between block siblings became anonymous blocks as they were collected, so `blocks` is
        // already in source order and nothing here has to reorder it.
        if (blocks.Count == 0)
        {
            box.Inlines.AddRange(inlines);
            return;
        }

        box.Children.AddRange(TableFixup(blocks, style));
    }

    /// <summary>
    /// Closes the run of inline content collected so far into an anonymous block, at the point a
    /// block-level sibling ends it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One anonymous block per contiguous run, appended in source order. Gathering every run in a
    /// container into a SINGLE anonymous block and putting it first is a whole line cheaper and is
    /// right only while the mixed case is leading text before the first block child — text AFTER
    /// one is hoisted above it, which reorders the document. <c>block/anonymous</c> measures it.
    /// </para>
    /// <para>
    /// Inline content that is nothing but collapsible white space generates no box at all. Without
    /// that rule the newline between two block elements — which is to say, the formatting of every
    /// readable HTML document — becomes an anonymous block holding one space, and that block is a
    /// full line tall. A document indented for legibility would gain a blank line before each of
    /// its sections. The run is cleared either way: whether or not it earned a box, it is over.
    /// </para>
    /// <para>
    /// A float or an absolute box does NOT close a run, and must not: it is out of flow, so the
    /// text either side of it belongs to one paragraph that flows around it. That is also what
    /// keeps the index each one recorded correct — a float declared inside a run belongs at the
    /// top of the anonymous block that run becomes, which is the child count it was recorded
    /// against before the run closed.
    /// </para>
    /// </remarks>
    static void CloseRun(List<LayoutBox> blocks, List<InlineItem> inlines, ComputedStyle parent)
    {
        if (inlines.Count == 0)
        {
            return;
        }

        if (inlines.Any(_ => HasContent(_, parent)))
        {
            var anonymous = new LayoutBox
            {
                Style = Anonymous(parent)
            };
            anonymous.Inlines.AddRange(inlines);
            blocks.Add(anonymous);
        }

        inlines.Clear();
    }

    /// <summary>
    /// Adds an <c>&lt;img&gt;</c>, as an atomic inline or as a block-level replaced box.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An image whose source cannot be resolved generates NO box, matching what a browser does
    /// with a broken <c>src</c> and no <c>alt</c> text: the element collapses rather than leaving
    /// an empty frame. Returning early is therefore correct behaviour, not a failure path.
    /// </para>
    /// <para>
    /// The <c>width</c> and <c>height</c> content attributes are presentational hints — they act
    /// as author-origin declarations of lowest priority, so any stylesheet rule beats them. They
    /// are read here rather than in the cascade because AngleSharp does not surface them as
    /// declarations, and without them the very common
    /// <c>&lt;img width="100"&gt;</c> sizes to its intrinsic width instead.
    /// </para>
    /// </remarks>
    static void AddImage(
        IElement element,
        ComputedStyle style,
        ComputedStyle parentStyle,
        DocumentContext context,
        List<LayoutBox> blocks,
        List<InlineItem> inlines,
        List<FloatChild> floats,
        List<FloatChild> positioned,
        string? link)
    {
        var source = element.GetAttribute("src");

        if (string.IsNullOrWhiteSpace(source))
        {
            Diagnostic.Image(context.OnDiagnostic, element.LocalName, source, "has no source to resolve");
            return;
        }

        if (context.Images.Resolve(source, out var reason) is not { } image)
        {
            // Worth reporting even though a browser with a broken src also draws nothing: here the
            // cause is as likely to be policy or the resolver as a genuinely missing file, and
            // those are indistinguishable from the output.
            Diagnostic.Image(context.OnDiagnostic, element.LocalName, source, reason);
            return;
        }

        var sized = WithAttributeSize(element, style);
        var selector = SelectorPath.For(element);

        if (style.Display == DisplayKind.Block)
        {
            var box = new LayoutBox
            {
                Style = sized,
                Element = element,
                Selector = selector,
                Image = image
            };

            // A floated image is the commonest float in real documents, and it arrives here rather
            // than through the general element path because an <img> never has children to build.
            if (sized.IsFloating)
            {
                floats.Add(new(box, blocks.Count));
                return;
            }

            if (sized.IsAbsolute)
            {
                positioned.Add(new(box, blocks.Count));
                return;
            }

            CloseRun(blocks, inlines, parentStyle);
            blocks.Add(box);
            return;
        }

        inlines.Add(new("", sized, selector, Image: image, Link: link));
    }

    /// <summary>
    /// Applies the <c>width</c> and <c>height</c> content attributes, where the stylesheet did not
    /// already set them.
    /// </summary>
    static ComputedStyle WithAttributeSize(IElement element, ComputedStyle style)
    {
        var width = style.Width;
        var height = style.Height;

        if (width.IsAuto && Attribute(element, "width") is { } attributeWidth)
        {
            width = attributeWidth;
        }

        if (height.IsAuto && Attribute(element, "height") is { } attributeHeight)
        {
            height = attributeHeight;
        }

        if (width == style.Width && height == style.Height)
        {
            return style;
        }

        return style with
        {
            Width = width,
            Height = height
        };
    }

    static CssLength? Attribute(IElement element, string name)
    {
        var value = element.GetAttribute(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // A bare number is pixels, per the HTML standard's rules for dimension attributes; a
        // trailing percent sign is the one unit they accept.
        var text = value.AsSpan().Trim();

        if (text.EndsWith('%'))
        {
            if (CssValues.TryParseNumber(text[..^1], out var percent))
            {
                return CssLength.Percentage(percent);
            }

            return null;
        }

        if (CssValues.TryParseNumber(text, out var pixels))
        {
            return CssLength.Pixels(pixels);
        }

        return null;
    }

    /// <summary>
    /// Whether an inline item survives white-space collapsing.
    /// </summary>
    /// <remarks>
    /// A forced break always counts — <c>&lt;br&gt;</c> between two blocks is content even though
    /// it carries no characters, and so is an image. Under a preserving white-space value, so does
    /// a space.
    /// </remarks>
    static bool HasContent(InlineItem item, ComputedStyle style) =>
        item.ForcedBreak ||
        item.Image is not null ||
        style.PreservesSpaces ||
        item.Text.AsSpan().TrimStart(" \n").Length > 0;

    static void Collect(
        INode node,
        ComputedStyle parentStyle,
        DocumentContext context,
        List<LayoutBox> blocks,
        List<InlineItem> inlines,
        List<FloatChild> floats,
        List<FloatChild> positioned,
        string? link,
        ListNumbering numbering)
    {
        if (node is IText text)
        {
            // Cased after white-space processing and before anything measures it, so the
            // text that is shaped, broken into lines and painted is the same string throughout.
            var content = TextTransform.Apply(
                WhiteSpace.Process(text.Data, parentStyle),
                parentStyle);
            if (content.Length > 0)
            {
                inlines.Add(new(content, StyleResolver.ForText(parentStyle), null, Link: link));
            }

            return;
        }

        if (node is not IElement element)
        {
            return;
        }

        var style = StyleResolver.Resolve(element, parentStyle, context);

        // A <col> or <colgroup> describes columns rather than generating one. Laying its (empty)
        // content out as a block would put a stray box in the middle of the table.
        if (style.Display is DisplayKind.None or DisplayKind.TableColumn)
        {
            // A <col> or <colgroup> describes columns rather than generating one, and its width DOES
            // reach column sizing — through `LayoutBox.Columns` on the table, since there is no box
            // of its own to hang it from. Recorded here rather than in the grid because this is
            // where the cascade has already been run over the element.
            if (style.Display == DisplayKind.TableColumn)
            {
                // A <colgroup> holding <col> children describes its columns through them, so it
                // contributes nothing of its own and the children are walked instead. Walked HERE
                // rather than through the ordinary child loop, which this branch returns before
                // reaching — missing that left every <col> in the document unread while a bare
                // <colgroup span> worked, and the two look alike enough that the difference was
                // only visible against a browser.
                var children = element.Children
                    .Where(_ => _.LocalName.Equals("col", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (children.Count > 0)
                {
                    var before = context.PendingColumns.Count;

                    foreach (var child in children)
                    {
                        Collect(child, style, context, blocks, inlines, floats, positioned, link, numbering);
                    }

                    // The group covers whatever its children added, which is what a browser reports
                    // a rectangle for — one spanning all of them rather than nothing at all.
                    context.PendingColumnBoxes.Add(new(
                        SelectorPath.For(element),
                        before,
                        context.PendingColumns.Count - before));

                    return;
                }

                Columns(element, style, context);
            }

            return;
        }

        // After the display check, so an element a browser draws nothing for either reports
        // nothing: `display: none` is not a loss.
        if (context.Reports)
        {
            UnsupportedAttributes.Report(element, context.OnDiagnostic);
        }

        // `float` does not apply to the boxes inside a table. Honouring it would take a row or a
        // cell out of the grid it belongs to, so the table would lay out around a hole and the
        // content would be positioned as though it were a block of its own — content corruption
        // from one declaration CSS says to ignore. A whole `display: table` still floats.
        if (style is {IsFloating: true, IsTablePart: true} && style.Display != DisplayKind.Table)
        {
            style = style with {Float = FloatKind.None};
        }

        // CSS 2.1 §9.7: absolute positioning wins over floating outright, and both make a box
        // block-level whatever `display` asked for — so `float: left` or `position: absolute` on a
        // <span> generates a block box rather than runs in a line. Table and list-item displays are
        // already block-level and keep their own layout mode.
        if (style.IsAbsolute)
        {
            style = style with {Float = FloatKind.None};
        }

        if (style is {Display: DisplayKind.Inline or DisplayKind.InlineBlock} and
            ({IsFloating: true} or {IsAbsolute: true}))
        {
            style = style with {Display = DisplayKind.Block};
        }

        // An anchor sets the link for its whole subtree. Nested anchors are invalid HTML and
        // AngleSharp's parser unnests them, so the innermost simply wins here.
        if (element.LocalName.Equals("a", StringComparison.OrdinalIgnoreCase) &&
            element.GetAttribute("href") is {Length: > 0} href)
        {
            link = href;
        }

        // A line break carries no text and no box of its own; it ends the line it is on. Handled
        // ahead of the display switch because white-space processing would otherwise turn its
        // newline into a collapsible space and lose it.
        if (UserAgentStyles.IsLineBreak(element.LocalName))
        {
            inlines.Add(new("", style, SelectorPath.For(element), ForcedBreak: true));
            return;
        }

        if (element.LocalName.Equals("img", StringComparison.OrdinalIgnoreCase))
        {
            AddImage(element, style, parentStyle, context, blocks, inlines, floats, positioned, link);
            return;
        }

        if (style.Display == DisplayKind.Inline)
        {
            // An inline element contributes its runs to the line being built rather than a box of
            // its own. Backgrounds and borders on inlines are not painted yet; the element still
            // carries a selector so its geometry becomes comparable once they are.
            var selector = SelectorPath.For(element);

            // The element's opening edge, carrying its left padding and border. Emitted only when
            // there is a surround to carry, so a document full of plain <span>s produces none of
            // these and the tokeniser never sees the case.
            if (style.HasSurround)
            {
                inlines.Add(new("", style, selector, Edge: InlineEdgeKind.Leading));
            }

            var before = inlines.Count;

            // An inline element gets its generated content here rather than through `AddChildren`,
            // which it never reaches — it contributes runs to the line being built instead of a box
            // of its own. Missing this is what left `<q>` without its quotation marks, since
            // `q::before` is where those come from.
            //
            // Counters are entered here too, for the same reason: an inline element declaring
            // `counter-increment` has to affect the counters its own content reads.
            var counters = context.Counters.Enter(style);

            Generated(element, "before", style, context, inlines, link);

            foreach (var child in element.ChildNodes)
            {
                Collect(child, style, context, blocks, inlines, floats, positioned, link, numbering);
            }

            Generated(element, "after", style, context, inlines, link);
            context.Counters.Leave(counters);

            // Only the runs this recursion added, and only those no nested inline has already
            // claimed. Rescanning the whole list would relabel a preceding sibling's runs.
            for (var index = before; index < inlines.Count; index++)
            {
                if (inlines[index].Selector is null)
                {
                    // This element's own text, which carries this element's style already — so its
                    // background is painted from that rather than recorded as a backdrop.
                    inlines[index] = inlines[index] with {Selector = selector};
                    continue;
                }

                // A run belonging to a NESTED inline element. Its own style is the nested one's,
                // so without recording this one there is nothing to paint its background from —
                // which is what makes `<mark>a <b>b</b></mark>` lose its highlight behind the bold
                // word — and nothing to report its geometry from either.
                //
                // Recorded whether or not it paints anything, because the box dump needs the chain
                // regardless: a browser reports an enclosing inline at its OWN height, so a tall
                // element wrapping a short one cannot be measured from the short one's runs.
                inlines[index] = inlines[index] with
                {
                    Backdrops = [style, .. inlines[index].Backdrops ?? []]
                };
            }

            if (style.HasSurround)
            {
                inlines.Add(new("", style, selector, Edge: InlineEdgeKind.Trailing));
            }

            return;
        }

        var box = new LayoutBox
        {
            Style = style,
            Element = element,
            Selector = SelectorPath.For(element),
            Marker = Marker(element, style, numbering)
        };

        AddChildren(box, element, style, context, link);

        if (style.IsFloating)
        {
            // Recorded against the number of in-flow siblings already collected, which is where in
            // the flow the float was declared and therefore how far down the page it starts.
            floats.Add(new(box, blocks.Count));
            return;
        }

        if (style.IsAbsolute)
        {
            // Same index, for a different reason: an absolute box with auto offsets goes where
            // flow would have put it, so the flow position it was declared at has to survive even
            // though the box never takes part in flow.
            positioned.Add(new(box, blocks.Count));
            return;
        }

        // An atomic inline joins the line rather than the flow, so it does NOT close the run: the
        // text before and after it belongs to the same paragraph, exactly as it does around an
        // image. Its own contents were collected the way a block's are, which is the whole of what
        // inline-block means.
        if (style.IsAtomicInline)
        {
            inlines.Add(new("", style, SelectorPath.For(element), Box: box, Link: link));
            return;
        }

        // The one thing that ends a run of inline content, which is why this is the only place
        // besides the end of the container that closes one.
        CloseRun(blocks, inlines, parentStyle);
        blocks.Add(box);
    }

    /// <summary>
    /// Wraps children a table or row cannot hold in the anonymous boxes CSS requires.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Unreachable from HTML, and reachable from CSS. The HTML parser guarantees a table's children
    /// are sections and rows and that a row's children are cells — it moves anything else out of
    /// the table entirely. But <c>display: table</c> in a stylesheet can put arbitrary blocks
    /// inside one, and a table lays out its children by role rather than in order, so a child with
    /// no role would simply never be positioned or painted.
    /// </para>
    /// <para>
    /// Silently losing content is the one outcome worth going out of the way to avoid, which is why
    /// this exists despite nothing in the corpus reaching it. The wrapper's geometry is not measured
    /// against a browser and may not match one; the content being on the page at all is the point.
    /// </para>
    /// </remarks>
    static List<LayoutBox> TableFixup(List<LayoutBox> blocks, ComputedStyle parent)
    {
        var wrapper = parent.Display switch
        {
            DisplayKind.Table => DisplayKind.TableRow,
            DisplayKind.TableHeaderGroup or DisplayKind.TableRowGroup or DisplayKind.TableFooterGroup =>
                DisplayKind.TableRow,
            DisplayKind.TableRow => DisplayKind.TableCell,
            _ => DisplayKind.None
        };

        if (wrapper == DisplayKind.None ||
            blocks.All(_ => Belongs(_.Style.Display, parent.Display)))
        {
            return blocks;
        }

        var result = new List<LayoutBox>();
        var stray = new List<LayoutBox>();

        foreach (var child in blocks)
        {
            if (Belongs(child.Style.Display, parent.Display))
            {
                Flush();
                result.Add(child);
                continue;
            }

            stray.Add(child);
        }

        Flush();
        return result;

        void Flush()
        {
            if (stray.Count == 0)
            {
                return;
            }

            // A table needs two levels of wrapper — a row holding a cell — where a row needs only
            // the cell.
            var inner = new LayoutBox
            {
                Style = Anonymous(parent) with
                {
                    Display = DisplayKind.TableCell
                }
            };
            inner.Children.AddRange(stray);

            if (wrapper == DisplayKind.TableCell)
            {
                result.Add(inner);
            }
            else
            {
                var row = new LayoutBox
                {
                    Style = Anonymous(parent) with
                    {
                        Display = DisplayKind.TableRow
                    }
                };
                row.Children.Add(inner);
                result.Add(row);
            }

            stray.Clear();
        }
    }

    /// <summary>Whether a child's display is one its parent's table role can hold.</summary>
    static bool Belongs(DisplayKind child, DisplayKind parent) =>
        parent switch
        {
            DisplayKind.Table => child is
                DisplayKind.TableCaption or
                DisplayKind.TableHeaderGroup or
                DisplayKind.TableRowGroup or
                DisplayKind.TableFooterGroup or
                DisplayKind.TableRow,
            DisplayKind.TableHeaderGroup or
                DisplayKind.TableRowGroup or
                DisplayKind.TableFooterGroup =>
                child == DisplayKind.TableRow,
            DisplayKind.TableRow => child == DisplayKind.TableCell,
            _ => true
        };

    /// <summary>
    /// The marker a box shows, and null when it is not a list item or shows none.
    /// </summary>
    /// <remarks>
    /// The counter advances even for an item whose <c>list-style-type</c> is <c>none</c>, which is
    /// what CSS requires and what keeps a deliberately unmarked item from renumbering the ones
    /// after it.
    /// </remarks>
    static ListMarker? Marker(IElement element, ComputedStyle style, ListNumbering numbering)
    {
        if (style.Display != DisplayKind.ListItem)
        {
            return null;
        }

        var ordinal = numbering.Take(element);

        if (style.ListStyle == ListStyleKind.None)
        {
            return null;
        }

        // A marker image replaces the counter style entirely, and is drawn as inline content by
        // `AddChildren` rather than as a marker here. It falls back to this when the source did not
        // resolve — measured, and the reason the check is on the RESOLVED image rather than on the
        // declaration.
        if (style.MarkerImage is not null)
        {
            return null;
        }

        return new()
        {
            Kind = style.ListStyle,
            Ordinal = ordinal
        };
    }

    /// <summary>
    /// Adds the content of one of an element's pseudo-elements, if it has any.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generated content is INLINE content of the host element rather than a box beside it, which
    /// is what makes <c>::before</c> sit on the host's first line and share its line box. It is
    /// added through the same list the host's own text goes into, so the run-closing that turns
    /// mixed content into anonymous blocks applies to it without knowing it is generated.
    /// </para>
    /// <para>
    /// A pseudo-element has no element and therefore no selector, so the geometry comparison cannot
    /// see it directly — a browser's <c>getBoundingClientRect()</c> cannot either. What it CAN see
    /// is the effect: generated content changes the host's own box, so a scenario measures it
    /// through the element it was added to.
    /// </para>
    /// <para>
    /// A pseudo asking to be a BLOCK gets a box of its own instead, appended to the host's block
    /// children rather than to its line. The run is closed first, so a <c>::before</c> lands above
    /// the host's own text and an <c>::after</c> below it — and the box carries the pseudo's own
    /// style, so its margins, its <c>clear</c> and its height are the ordinary block ones. That is
    /// what makes the oldest idiom in CSS work: <c>content: ""; display: block; clear: both</c> on
    /// an <c>::after</c>, which is how a container was made to enclose its floats before
    /// <c>overflow</c> was used for it.
    /// </para>
    /// <para>
    /// Only where the host HAS block children to append to. An inline host contributes runs to a
    /// line rather than a list of boxes, so a block pseudo inside one would have to blockify the
    /// host — and that is reported instead.
    /// </para>
    /// </remarks>
    static void Generated(
        IElement element,
        string pseudo,
        ComputedStyle host,
        DocumentContext context,
        List<InlineItem> inlines,
        string? link,
        List<LayoutBox>? blocks = null)
    {
        if (StyleResolver.ResolvePseudo(element, pseudo, host, context) is not var (style, content))
        {
            return;
        }

        var block = blocks is not null && style.Display is DisplayKind.Block or DisplayKind.ListItem;

        if (style.Display != DisplayKind.Inline && !block && context.Reports)
        {
            Diagnostic.Property(
                context.OnDiagnostic,
                $"{element.LocalName}::{pseudo}",
                "display",
                style.Display.ToString().ToLowerInvariant(),
                "generated content is laid out as inline content of its host");
        }

        // Where the content goes: the host's own line, or a box of the pseudo's own. In source
        // order either way — closing the run first is what puts a block `::after` below the host's
        // text rather than above it.
        var target = inlines;

        if (block)
        {
            CloseRun(blocks!, inlines, host);
            target = [];
        }

        // The pseudo's own padding and border, exactly as an inline element's are. Its edges carry
        // no selector, having no element to name — which costs nothing, since a selector is only
        // ever read by the box dump and a browser reports no rectangle for a pseudo-element either.
        //
        // Not for a block pseudo: its box carries the pseudo's style directly, so the surround is
        // already the box's own and adding it again would apply it twice.
        if (style.HasSurround && !block)
        {
            target.Add(new("", style, null, Edge: InlineEdgeKind.Leading));
        }

        var text = new StringBuilder();

        foreach (var item in content)
        {
            switch (item.Kind)
            {
                case ContentKind.Text:
                    text.Append(item.Text);
                    break;

                case ContentKind.Attribute:
                    text.Append(element.GetAttribute(item.Text) ?? "");
                    break;

                case ContentKind.Counter:
                    text.Append(ListMarkers.Counter(item.Style, context.Counters.Value(item.Text)));
                    break;

                case ContentKind.Counters:
                    text.Append(string.Join(
                        item.Separator,
                        context.Counters.Values(item.Text)
                            .Select(_ => ListMarkers.Counter(item.Style, _))));
                    break;

                case ContentKind.Quote:
                    text.Append(Quote(item, style, context));
                    break;

                case ContentKind.Image:
                    Flush();

                    // Resolved through the same store an <img src> and a background url() go
                    // through, so a stylesheet naming an image is bound by the same policy. An
                    // image that does not resolve contributes nothing, as one in the markup does.
                    if (context.Images.Resolve(item.Text, out var reason) is { } image)
                    {
                        target.Add(new("", style, null, Image: image, Link: link));
                    }
                    else if (context.Reports)
                    {
                        Diagnostic.Image(
                            context.OnDiagnostic,
                            $"{element.LocalName}::{pseudo}",
                            item.Text,
                            reason);
                    }

                    break;
            }
        }

        Flush();

        if (style.HasSurround && !block)
        {
            target.Add(new("", style, null, Edge: InlineEdgeKind.Trailing));
        }

        if (block)
        {
            // Appended even when it holds nothing. `content: ""` is the whole of the clearfix
            // idiom, and a box dropped for being empty is a box whose margins and `clear` are
            // dropped with it — which is the only thing that idiom asks for.
            var own = new LayoutBox
            {
                Style = style
            };

            own.Inlines.AddRange(target);
            blocks!.Add(own);
        }

        void Flush()
        {
            if (text.Length == 0)
            {
                return;
            }

            var processed = WhiteSpace.Process(text.ToString(), style);
            text.Clear();

            if (processed.Length > 0)
            {
                // NOT generated when the pseudo took a box of its own. The flag exists to tell the
                // painter that a run has a background to fill even though it has no element to name
                // one — and a block pseudo's box has already filled it, so the flag would fill it
                // twice and draw the box's border a second time inside its own padding.
                target.Add(new(processed, style, null, Link: link, Generated: !block));
            }
        }
    }

    /// <summary>
    /// The quotation mark one quote keyword draws, and its effect on the nesting depth.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The depth is document-wide, which is CSS's rule: the marks depend on how many quotations are
    /// open anywhere above, so nesting one inside another changes its marks. An opening keyword
    /// reads the pair at the current depth and then descends; a closing one ascends first and reads
    /// the pair it arrives at, so a matched pair uses one pair of marks.
    /// </para>
    /// <para>
    /// A depth past the end of the <c>quotes</c> list reuses the LAST pair rather than running out,
    /// which is what keeps a deeply nested quotation from losing its marks. An empty list — from
    /// <c>quotes: none</c> — draws nothing while still tracking the depth.
    /// </para>
    /// </remarks>
    static string Quote(ContentItem item, ComputedStyle style, DocumentContext context)
    {
        var suppressed = item.Text == "\0";

        if (!item.Opening)
        {
            context.QuoteDepth = Math.Max(0, context.QuoteDepth - 1);
        }

        var mark = "";

        if (!suppressed && style.Quotes.Length >= 2)
        {
            var pairs = style.Quotes.Length / 2;
            var pair = Math.Min(context.QuoteDepth, pairs - 1);

            mark = style.Quotes[pair * 2 + (item.Opening ? 0 : 1)];
        }

        if (item.Opening)
        {
            context.QuoteDepth++;
        }

        return mark;
    }

    /// <summary>
    /// Records the widths a <c>&lt;col&gt;</c> or <c>&lt;colgroup&gt;</c> declares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A column definition generates no box, so its width has nowhere of its own to live and is
    /// appended to a list on the table it belongs to. The list is positional: one entry per column
    /// the definitions cover, in order, which is what lets the grid read the nth column's width
    /// without knowing anything about the elements that declared it.
    /// </para>
    /// <para>
    /// <c>span</c> repeats the width rather than sharing it out — <c>&lt;col span="3"&gt;</c> gives
    /// each of three columns that width, which is what the HTML Standard says and what a table
    /// author expects.
    /// </para>
    /// </remarks>
    static void Columns(IElement element, ComputedStyle style, DocumentContext context)
    {
        int span;
        if (int.TryParse(
                element.GetAttribute("span"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var declared) && declared > 0)
        {
            span = declared;
        }
        else
        {
            span = 1;
        }

        // Appended to whatever this table has collected so far, and attached to the table box once
        // every child has been walked.
        context.PendingColumnBoxes
            .Add(
                new(
            SelectorPath.For(element),
            context.PendingColumns.Count,
            span));

        for (var index = 0; index < span; index++)
        {
            context.PendingColumns.Add(style.Width);
        }
    }

    /// <summary>
    /// The style for an anonymous block box: inherited properties only.
    /// </summary>
    /// <remarks>
    /// CSS says an anonymous box inherits from its parent and takes the initial value for every
    /// non-inherited property — so no margin, no padding, no border and no background, which is
    /// what stops a wrapper from painting a second copy of its parent's decoration.
    /// </remarks>
    static ComputedStyle Anonymous(ComputedStyle parent) =>
        new()
        {
            Display = DisplayKind.Block,
            Color = parent.Color,
            FontFamilies = parent.FontFamilies,
            FontSize = parent.FontSize,
            FontWeight = parent.FontWeight,
            Italic = parent.Italic,
            LineHeight = parent.LineHeight,
            ListStyle = parent.ListStyle,
            BorderSpacingX = parent.BorderSpacingX,
            BorderSpacingY = parent.BorderSpacingY,
            VerticalAlign = parent.VerticalAlign,
            TextAlign = parent.TextAlign,
            WhiteSpace = parent.WhiteSpace
        };
}