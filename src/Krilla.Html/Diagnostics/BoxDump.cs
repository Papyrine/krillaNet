namespace Krilla.Html.Diagnostics;

/// <summary>
/// Extracts element geometry from a laid-out tree, for comparison against a browser.
/// </summary>
/// <remarks>
/// <para>
/// This is the primary fidelity signal, and it earns that place by being exact. A pixel diff of a
/// page of text plateaus somewhere short of identical however correct the layout is, because two
/// rasterisers do not antialias a glyph edge the same way; a box comparison has no such floor. It
/// also localises: "this paragraph is 3px too low" is something to act on, where "the page is 4%
/// different" is not.
/// </para>
/// <para>
/// Border boxes, because that is what <c>getBoundingClientRect()</c> returns. Anonymous boxes are
/// omitted — they correspond to no element, so the browser has nothing to compare them against.
/// </para>
/// </remarks>
public static class BoxDump
{
    /// <summary>
    /// Every element box in <paramref name="root"/>, in document order.
    /// </summary>
    /// <remarks>
    /// Includes images laid out on a line, which have no <see cref="LayoutBox"/> of their own
    /// because they are inline-level. Their geometry is known exactly all the same, so reporting it
    /// is what lets the corpus measure a replaced element in flow rather than recording it as
    /// something the engine did not produce.
    /// </remarks>
    internal static List<BoxGeometry> Collect(LayoutBox root)
    {
        var boxes = new List<BoxGeometry>();

        // Inline elements have no box of their own, so their geometry is accumulated from the text
        // runs they produced and added once the whole tree is walked. Keyed by selector because an
        // inline that wraps has one fragment per line and the browser reports their union.
        var inlines = new Dictionary<string, Rect>();

        Walk(root, null);

        // Anything that also produced a real box is dropped rather than reported twice: an
        // inline-block, a float and a table cell all generate text runs AND a LayoutBox, and the
        // box is the better answer — it carries the borders and padding the runs do not.
        var placed = boxes.Select(_ => _.Selector).ToHashSet(StringComparer.Ordinal);

        foreach (var (selector, rect) in inlines)
        {
            if (!placed.Contains(selector))
            {
                boxes.Add(Geometry(selector, rect));
            }
        }

        return boxes;

        // Recursive rather than over `Descendants()`, because a transform has to accumulate down
        // the tree: a transformed box inside a transformed one carries both, and a flat walk has
        // nowhere to keep the matrix that says so.
        void Walk(LayoutBox box, Matrix3x2? inherited)
        {
            var matrix = inherited;

            if (box.Style.Transform is {} transform)
            {
                var own = transform.Resolve(box.BorderBox);
                matrix = inherited is {} outer ? CssTransform.Combine(outer, own) : own;
            }

            if (box.Selector is {} selector)
            {
                boxes.Add(Geometry(selector, Visual(box.BorderBox, matrix)));
            }

            // A <wbr> generates no box, and a browser says so by returning an empty rectangle at
            // the origin rather than one where the element sits. Reported from the INLINE ITEMS
            // rather than from a line, because it produces no token to hang off one — and it has to
            // be reported at all, or every document containing one has an element the geometry
            // comparison counts as unmatched.
            foreach (var item in box.Inlines)
            {
                if (item is {SoftBreak: true, Selector: {} empty})
                {
                    boxes.Add(Geometry(empty, default));
                }
            }

            foreach (var line in box.Lines)
            {
                foreach (var run in line.Runs)
                {
                    Fragment(run.Selector, Rectangle(run), run.Backdrops, run.Y, matrix);
                }

                // An inline element's padding and border are part of the rectangle a browser
                // reports for it, and no text run reaches them — so without these an element with
                // `padding: 1px 4px` measures 8px narrow and 2px short.
                foreach (var edge in line.Edges)
                {
                    Fragment(edge.Selector, edge.Bounds, edge.Ancestors, edge.Baseline, matrix);
                }

                foreach (var image in line.Images)
                {
                    if (image.Selector is {} imageSelector)
                    {
                        boxes.Add(Geometry(imageSelector, Visual(image.Bounds, matrix)));
                    }
                }

                foreach (var ended in line.Breaks)
                {
                    boxes.Add(Geometry(ended.Selector, Visual(ended.Bounds, matrix)));
                }

                foreach (var atomic in line.Boxes)
                {
                    Walk(atomic, matrix);
                }
            }

            // A column definition generates no box, and a browser reports a rectangle for it all the
            // same — its columns' extent by the height of the row area. Without these the geometry
            // comparison counts every <col> in the document as an element this engine did not
            // produce.
            foreach (var column in box.ColumnBoxes)
            {
                boxes.Add(Geometry(column.Selector, Visual(column.Bounds, matrix)));
            }

            foreach (var child in box.Children)
            {
                Walk(child, matrix);
            }

            foreach (var floated in box.Floats)
            {
                Walk(floated.Box, matrix);
            }

            foreach (var positioned in box.Positioned)
            {
                Walk(positioned.Box, matrix);
            }
        }

        // Unions one run into the element that produced it AND into every inline ancestor of that
        // element, because a browser's rectangle for `<em>` covers the `<b>` nested inside it. The
        // ancestors are reachable without any extra bookkeeping: a selector path is its own
        // ancestry, so every prefix of it names one. Prefixes that turn out to be block-level are
        // filtered above, having produced a box of their own.
        // The run's Y is its baseline, and the rectangle a browser reports around inline text
        // reaches the font's whole-pixel ascent above it and its whole-pixel descent below — 17px
        // tall for 16px Liberation Sans and 14px for the same face at 12px, neither of which is a
        // fixed ratio of the size. It is NOT the leading box: `line-height` moves the fragments
        // apart without making any of them taller.
        static Rect Rectangle(TextRun run)
        {
            var size = run.Style.FontSize;
            var ascent = run.Face.Ascent(size);

            return new(run.X, run.Y - ascent, run.Width, ascent + run.Face.Descent(size));
        }

        void Fragment(
            string? selector,
            Rect bounds,
            IReadOnlyList<InlineBackdrop>? ancestors,
            float baseline,
            Matrix3x2? matrix)
        {
            if (selector is null)
            {
                return;
            }

            var path = selector;

            // The innermost enclosing inline element is LAST in the chain, and the walk goes
            // outward, so the index counts down alongside the prefixes.
            var index = ancestors?.Count ?? 0;

            Add(path, bounds);

            while (true)
            {
                var cut = path.LastIndexOf(" > ", StringComparison.Ordinal);
                if (cut < 0)
                {
                    return;
                }

                path = path[..cut];
                index--;

                // An enclosing inline element is reported at ITS OWN height rather than at the
                // height of whatever is nested inside it: a browser's rectangle for a plain
                // <span> holding a bordered one stops at the span's own text box, and taking the
                // union of the two vertically is two pixels too tall at each end. Horizontally the
                // union is right, which is why only the vertical span is replaced.
                //
                // Past the inline ancestry the prefixes are block-level and have boxes of their
                // own, so whatever is accumulated for them here is discarded.
                Add(
                    path,
                    index >= 0 && ancestors is not null
                        ? InlineMetrics.Reframe(bounds, ancestors[index].Style, ancestors[index].Face, baseline)
                        : bounds);
            }

            void Add(string key, Rect rect)
            {
                var visual = Visual(rect, matrix);

                inlines[key] = inlines.TryGetValue(key, out var existing)
                    ? Union(existing, visual)
                    : visual;
            }
        }
    }

    /// <summary>The smallest rectangle containing both.</summary>
    static Rect Union(Rect first, Rect second)
    {
        var left = MathF.Min(first.X, second.X);
        var top = MathF.Min(first.Y, second.Y);

        return new(
            left,
            top,
            MathF.Max(first.Right, second.Right) - left,
            MathF.Max(first.Bottom, second.Bottom) - top);
    }

    /// <summary>
    /// A box's rectangle as a browser reports it: transformed, when a transform applies.
    /// </summary>
    /// <remarks>
    /// <c>getBoundingClientRect()</c> returns the VISUAL rectangle, so a rotated 60x40 tile comes
    /// back as the 71.96x64.6 box that encloses it. Reporting the untransformed layout box instead
    /// would make every transformed element in the corpus look like a defect, and would leave the
    /// transform arithmetic measured by nothing but pixels.
    /// </remarks>
    static Rect Visual(Rect rect, Matrix3x2? matrix)
    {
        if (matrix is {} applied)
        {
            return CssTransform.Bounds(applied, rect);
        }

        return rect;
    }

    static BoxGeometry Geometry(string selector, Rect rect) =>
        new(selector, Round(rect.X), Round(rect.Y), Round(rect.Width), Round(rect.Height));

    /// <summary>
    /// Lays out <paramref name="html"/> and returns its element geometry, without producing a PDF.
    /// </summary>
    public static async Task<IReadOnlyList<BoxGeometry>> MeasureAsync(
        string html,
        HtmlOptions options,
        Cancel cancel = default)
    {
        using var document = await HtmlConverter
            .ParseAsync(html, options, cancel);

        // Through the same `@page` fold the conversion goes through, or a document declaring its own
        // paper would be measured against one rectangle and painted against another.
        using var layout = HtmlConverter.LayoutDocument(
            document,
            HtmlConverter.Paged(document, options, out _));

        return Collect(layout.Root);
    }

    /// <summary>
    /// Rounds to two decimals.
    /// </summary>
    /// <remarks>
    /// Layout is float arithmetic and a browser's rects are doubles, so the last bits will never
    /// agree and comparing them raw would report differences of 1e-5 as though they meant
    /// something. Two decimals is finer than any real layout error and coarser than the noise.
    /// </remarks>
    static float Round(float value) =>
        MathF.Round(value, 2);
}
