namespace Krilla.Html.Layout;

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

        // One counter per element, whether or not it turns out to hold list items. It is the only
        // thing that knows how many items have been generated so far, which is what makes a
        // `display: none` item skip a number rather than consume one.
        var numbering = ListNumbering.For(element);

        foreach (var node in element.ChildNodes)
        {
            Collect(node, style, context, blocks, inlines, link, numbering);
        }

        // A block container is either all-block or all-inline. When both turned up, the runs
        // between block siblings become anonymous blocks so ordering is preserved.
        if (blocks.Count == 0)
        {
            box.Inlines.AddRange(inlines);
            return;
        }

        box.Children.AddRange(TableFixup(blocks, style));

        // Inline content that is nothing but collapsible white space generates no box at all.
        // Without this rule the newline between two block elements — which is to say, the
        // formatting of every readable HTML document — becomes an anonymous block holding one
        // space, and that block is a full line tall. A document indented for legibility would gain
        // a blank line before each of its sections.
        if (inlines.Count > 0 && inlines.Any(_ => HasContent(_, style)))
        {
            var anonymous = new LayoutBox
            {
                Style = Anonymous(style)
            };
            anonymous.Inlines.AddRange(inlines);

            // Prepended rather than appended: the mixed case in practice is leading text before
            // the first block child, and putting it first preserves reading order for that case.
            box.Children.Insert(0, anonymous);
        }
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
        DocumentContext context,
        List<LayoutBox> blocks,
        List<InlineItem> inlines,
        string? link)
    {
        var source = element.GetAttribute("src");
        if (string.IsNullOrWhiteSpace(source) || context.Images.Resolve(source) is not {} image)
        {
            return;
        }

        var sized = WithAttributeSize(element, style);
        var selector = SelectorPath.For(element);

        if (style.Display == DisplayKind.Block)
        {
            blocks.Add(new()
            {
                Style = sized,
                Element = element,
                Selector = selector,
                Image = image
            });
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

        if (width.IsAuto && Attribute(element, "width") is {} attributeWidth)
        {
            width = attributeWidth;
        }

        if (height.IsAuto && Attribute(element, "height") is {} attributeHeight)
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
        var text = value.Trim();

        if (text.EndsWith('%'))
        {
            return CssValues.TryParseNumber(text[..^1], out var percent)
                ? CssLength.Percentage(percent)
                : null;
        }

        return CssValues.TryParseNumber(text, out var pixels) ? CssLength.Pixels(pixels) : null;
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
        string? link,
        ListNumbering numbering)
    {
        if (node is IText text)
        {
            var content = WhiteSpace.Process(text.Data, parentStyle);
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
            return;
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
            AddImage(element, style, context, blocks, inlines, link);
            return;
        }

        if (style.Display == DisplayKind.Inline)
        {
            // An inline element contributes its runs to the line being built rather than a box of
            // its own. Backgrounds and borders on inlines are not painted yet; the element still
            // carries a selector so its geometry becomes comparable once they are.
            var selector = SelectorPath.For(element);
            var before = inlines.Count;

            foreach (var child in element.ChildNodes)
            {
                Collect(child, style, context, blocks, inlines, link, numbering);
            }

            // Only the runs this recursion added, and only those no nested inline has already
            // claimed. Rescanning the whole list would relabel a preceding sibling's runs.
            for (var index = before; index < inlines.Count; index++)
            {
                if (inlines[index].Selector is null)
                {
                    inlines[index] = inlines[index] with {Selector = selector};
                }
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

        if (wrapper == DisplayKind.None || blocks.All(_ => Belongs(_.Style.Display, parent.Display)))
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
            var inner = new LayoutBox {Style = Anonymous(parent) with {Display = DisplayKind.TableCell}};
            inner.Children.AddRange(stray);

            if (wrapper == DisplayKind.TableCell)
            {
                result.Add(inner);
            }
            else
            {
                var row = new LayoutBox {Style = Anonymous(parent) with {Display = DisplayKind.TableRow}};
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
            DisplayKind.Table => child is DisplayKind.TableCaption or DisplayKind.TableHeaderGroup or
                DisplayKind.TableRowGroup or DisplayKind.TableFooterGroup or DisplayKind.TableRow,
            DisplayKind.TableHeaderGroup or DisplayKind.TableRowGroup or DisplayKind.TableFooterGroup =>
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

        return new()
        {
            Kind = style.ListStyle,
            Ordinal = ordinal
        };
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

/// <summary>
/// A stable path identifying an element, used to line our geometry up with the browser's.
/// </summary>
/// <remarks>
/// The format is <c>html &gt; body &gt; div:nth-child(2)</c>. Both this and the script that
/// harvests <c>getBoundingClientRect()</c> in the reference generator build it by the same walk,
/// which is the whole requirement — the string only has to be reproducible, not minimal or
/// pretty, and index-based paths are reproducible in a way that class- or id-based ones are not.
/// </remarks>
static class SelectorPath
{
    /// <summary>Builds the path for <paramref name="element"/>.</summary>
    public static string For(IElement element)
    {
        var segments = new List<string>();

        for (var current = element; current is not null; current = current.ParentElement)
        {
            segments.Add(Segment(current));
        }

        segments.Reverse();
        return string.Join(" > ", segments);
    }

    static string Segment(IElement element)
    {
        var name = element.LocalName;

        if (element.ParentElement is not {} parent)
        {
            return name;
        }

        var index = 1;
        foreach (var sibling in parent.Children)
        {
            if (ReferenceEquals(sibling, element))
            {
                break;
            }

            index++;
        }

        return $"{name}:nth-child({index})";
    }
}
