namespace Krilla.Html.Layout;

/// <summary>
/// Lays out a page's <c>@page</c> margin boxes: the running headers, footers and page numbers a
/// document declares.
/// </summary>
/// <remarks>
/// <para>
/// This is the commonest reason a document has an <c>@page</c> rule at all, and the one thing a
/// converter cannot leave to the author to work around — no markup produces a footer, because
/// there is no element on the page to hang one from.
/// </para>
/// <para>
/// Per PAGE rather than once for the document, which is what <c>counter(page)</c> forces: a box
/// reading the page number has a different answer on every sheet, so its content, its layout and
/// its width are all settled while that sheet is being painted. They are small — a line or two of
/// text in a margin — so laying one out per page costs little, and caching them by content would
/// be caching the one thing that varies.
/// </para>
/// <para>
/// A margin box does NOT inherit from the document. CSS says its parent is the page context, so
/// the style here descends from the root font size and the root's font family, and from nothing
/// else: a <c>body { color: grey }</c> leaves the footer black, which is what a browser
/// implementing this would do and what an author writing <c>@page</c> expects. The family is the
/// one concession, and it is not inheritance — see <see cref="Build"/>.
/// </para>
/// </remarks>
static class PageMargins
{
    /// <summary>
    /// The laid-out margin boxes for one page, positioned in CSS pixels from its top-left corner.
    /// </summary>
    /// <param name="rules">The document's <c>@page</c> rules.</param>
    /// <param name="options">The page geometry, whose margins the boxes sit in.</param>
    /// <param name="document">The document, used only to make an element to carry a declaration.</param>
    /// <param name="context">The conversion's images, root size and diagnostic sink.</param>
    /// <param name="fonts">The faces available for measuring text.</param>
    /// <param name="number">This page's number, from one.</param>
    /// <param name="count">How many pages the document has, for <c>counter(pages)</c>.</param>
    /// <param name="blank">Whether a forced break left this page empty, for <c>:blank</c>.</param>
    /// <param name="name">
    /// The named page this sheet belongs to, from <c>page</c>, or null. What <c>@page cover</c>
    /// matches against.
    /// </param>
    /// <param name="strings">
    /// The named strings as they stand on THIS page, for <c>string()</c>. Empty by default, which
    /// is what a document declaring no <c>string-set</c> gets.
    /// </param>
    /// <param name="families">
    /// The root element's font families, which a margin box declaring none of its own falls back
    /// to. Not inheritance — the page context has no font of its own, and the only alternative is
    /// `FontSet.Fallback`, which in a set of Liberation faces is the MONOSPACE one. A running
    /// header in a typewriter font is not a defensible default, and a document has exactly one
    /// obvious answer to what its text looks like.
    /// </param>
    public static List<LayoutBox> Build(
        PageRules rules,
        HtmlOptions options,
        IDocument document,
        DocumentContext context,
        FontSet fonts,
        IReadOnlyList<string> families,
        int number,
        int count,
        bool blank,
        PageStrings strings = default,
        string? name = null)
    {
        var boxes = new List<LayoutBox>();

        if (rules.MarginBoxes.Count == 0)
        {
            return boxes;
        }

        foreach (var slot in Enum.GetValues<PageMarginSlot>())
        {
            if (Declarations(rules, slot, number, blank, name) is not {} declarations)
            {
                continue;
            }

            if (Box(declarations, slot, options, document, context, fonts, families, number, count, strings) is {} box)
            {
                boxes.Add(box);
            }
        }

        return boxes;
    }

    /// <summary>
    /// The declarations for one slot on one page, or null when no rule reaches it.
    /// </summary>
    /// <remarks>
    /// Every matching rule's text, CONCATENATED in cascade order rather than merged property by
    /// property. That is not a shortcut: a declaration block resolves later declarations over
    /// earlier ones, so joining the blocks and parsing the result once gives exactly the cascade,
    /// and gives it for shorthands too — which merging by property name would have to reimplement.
    /// </remarks>
    static string? Declarations(
        PageRules rules,
        PageMarginSlot slot,
        int number,
        bool blank,
        string? name)
    {
        var matching = rules.MarginBoxes
            .Where(_ => _.Slot == slot && _.Matches(number, blank, name))
            .OrderBy(_ => _.Specificity)
            .ThenBy(_ => _.Order)
            .ToList();

        if (matching.Count == 0)
        {
            return null;
        }

        return string.Join(';', matching.Select(_ => _.Declarations));
    }

    /// <summary>
    /// One laid-out margin box, or null when its <c>content</c> generates nothing.
    /// </summary>
    /// <remarks>
    /// <c>content</c> decides whether the box exists at all, which is CSS's own rule and a useful
    /// one: <c>@top-center { content: none }</c> on a selector is how a stylesheet takes the
    /// header off the title page.
    /// </remarks>
    static LayoutBox? Box(
        string declarations,
        PageMarginSlot slot,
        HtmlOptions options,
        IDocument document,
        DocumentContext context,
        FontSet fonts,
        IReadOnlyList<string> families,
        int number,
        int count,
        PageStrings strings)
    {
        var area = PageMarginSlots.Area(slot, options);

        if (area.Width <= 0 || area.Height <= 0)
        {
            return null;
        }

        // An element exists only to carry the declarations through the parser and the resolver.
        // It is never added to the document, so no selector in the stylesheet can reach it — which
        // is what keeps a margin box out of the document's cascade, as CSS asks.
        var element = document.CreateElement("div");
        element.SetAttribute("style", declarations);

        var declaration = element.GetStyle();

        // From the declaration TEXT when the parser rejected it, which `string()` makes it do:
        // AngleSharp drops the whole `content` declaration over one function it does not know, so
        // the literals around the string would go with it.
        var declared = declaration.GetPropertyValue("content") is {Length: > 0} parsed
            ? parsed
            : CssSource.Declaration(declarations, "content") ?? "";

        if (CssContent.Parse(declared) is not {} content)
        {
            // A value that named something and produced nothing is a gap rather than an absence,
            // and the realistic case is `string()` — the running-header mechanism CSS pairs with
            // `string-set`, which nothing here reads. Silence would leave the header missing with
            // nothing at all to say why.
            if (!string.IsNullOrWhiteSpace(declared) &&
                !declared.Equals("normal", StringComparison.OrdinalIgnoreCase) &&
                !declared.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                Diagnostic.Rule(context.OnDiagnostic, "@page", "content", declared, "no content is generated");
            }

            return null;
        }

        var parent = new ComputedStyle
        {
            FontSize = options.RootFontSize,
            FontFamilies = [.. families]
        };

        var style = StyleResolver.ForMarginBox(element, declaration, parent, context);

        // The slot's own name decides the alignment where the author did not: `@top-left` is flush
        // left and `@top-right` flush right, which is what makes "title on one side, page number
        // on the other" two rules and no alignment declarations.
        if (string.IsNullOrWhiteSpace(declaration.GetPropertyValue("text-align")))
        {
            style = style with
            {
                TextAlign = PageMarginSlots.Align(slot)
            };
        }

        style = style with
        {
            Display = DisplayKind.Block
        };

        var box = new LayoutBox
        {
            Style = style
        };

        Fill(box, content, style, context, number, count, strings);

        if (box.Inlines.Count == 0)
        {
            return null;
        }

        var used = BlockLayout.Layout(box, area.X, area.Y, area.Width, fonts, area.Width);

        // Placed in the strip after it has been measured, since where it sits depends on how tall
        // it turned out to be. A horizontal strip centres its box, which is what a running header
        // in a wide top margin wants; the vertical strips are where the three-way split is really
        // a split.
        var slack = Math.Max(0, area.Height - used);

        box.Translate(0, slack * Fraction(style, slot));

        return box;
    }

    /// <summary>
    /// Where in its strip the box sits, from zero at the top to one at the bottom.
    /// </summary>
    /// <remarks>
    /// A declared <c>vertical-align</c> wins, which is the property CSS Paged Media gives a margin
    /// box for exactly this. Its initial value is <c>baseline</c>, which has no meaning in a strip
    /// with nothing to share a baseline with, so that falls through to the slot's own default.
    /// </remarks>
    static float Fraction(ComputedStyle style, PageMarginSlot slot) =>
        style.VerticalAlign switch
        {
            VerticalAlignKind.Top => 0,
            VerticalAlignKind.Middle => 0.5f,
            VerticalAlignKind.Bottom => 1,
            _ => PageMarginSlots.Vertical(slot)
        };

    /// <summary>
    /// Turns a <c>content</c> value into the box's inline content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>counter(page)</c>, <c>counter(pages)</c> and <c>string()</c> are the three that only
    /// exist here — the page number, the page count and a named string as it stands on this sheet,
    /// which between them are the reason most authors reach for a margin box. The two counters go
    /// through the counter styles a list marker uses, so <c>counter(page, upper-roman)</c> numbers
    /// a preface the way a preface is numbered.
    /// </para>
    /// <para>
    /// Everything else in the grammar is either the same as it is on a pseudo-element or has no
    /// meaning here and is reported. A DOCUMENT counter has no value on a page — the page is not a
    /// position in the tree, so there is no scope stack to read — and <c>attr()</c> has no element
    /// to read an attribute from.
    /// </para>
    /// </remarks>
    static void Fill(
        LayoutBox box,
        List<ContentItem> content,
        ComputedStyle style,
        DocumentContext context,
        int number,
        int count,
        PageStrings strings)
    {
        var text = new StringBuilder();

        foreach (var item in content)
        {
            switch (item.Kind)
            {
                case ContentKind.Text:
                    text.Append(item.Text);
                    break;

                case ContentKind.Counter when item.Text.Equals("page", StringComparison.OrdinalIgnoreCase):
                    text.Append(ListMarkers.Counter(item.Style, number));
                    break;

                case ContentKind.Counter when item.Text.Equals("pages", StringComparison.OrdinalIgnoreCase):
                    text.Append(ListMarkers.Counter(item.Style, count));
                    break;

                case ContentKind.Counter:
                case ContentKind.Counters:
                    Report("content", $"counter({item.Text})", "a document counter has no value in a page margin box, so nothing is drawn for it");
                    break;

                case ContentKind.Attribute:
                    Report("content", $"attr({item.Text})", "a page margin box has no element to read an attribute from");
                    break;

                // The value the named string holds on THIS page, which is the whole point of the
                // mechanism: the same declaration reads differently on every sheet.
                case ContentKind.String:
                    text.Append(strings.Value(item.Text));
                    break;

                case ContentKind.Element:
                    Report("content", "content()", "a page margin box has no element of its own to take text from");
                    break;

                case ContentKind.Quote:
                    // The pair at depth zero, always: a margin box is not inside the document's
                    // quotations, so there is no nesting to track and nothing above it to be
                    // nested in.
                    if (style.Quotes.Length >= 2)
                    {
                        text.Append(style.Quotes[item.Opening ? 0 : 1]);
                    }

                    break;

                case ContentKind.Image:
                    Flush();

                    if (context.Images.Resolve(item.Text, out var reason) is {} image)
                    {
                        box.Inlines.Add(new("", style, null, Image: image));
                    }
                    else
                    {
                        Report("content", $"url({item.Text})", reason);
                    }

                    break;
            }
        }

        Flush();

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
                box.Inlines.Add(new(processed, StyleResolver.ForText(style), null, Generated: true));
            }
        }

        void Report(string name, string value, string reason) =>
            Diagnostic.Rule(context.OnDiagnostic, "@page", name, value, reason);
    }
}
