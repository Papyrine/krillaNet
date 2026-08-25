/// <summary>
/// Paints the laid-out box tree onto a krilla surface.
/// </summary>
/// <remarks>
/// <para>
/// Layout works in CSS pixels and PDF works in points, so every page pushes one scale transform
/// and everything below it is painted in layout units. Doing the conversion once, in the graphics
/// state, is what keeps the painting code free of unit arithmetic — and unit arithmetic scattered
/// across painting code is exactly how a renderer ends up almost right.
/// </para>
/// <para>
/// Paint order follows CSS 2.1 Appendix E as far as the feature set goes, and follows it as
/// PHASES over the whole page rather than as an order within each box: every background and border
/// first, then the floats, then all the inline content, then everything positioned. The
/// distinction is invisible until two boxes overlap and decisive the moment they do — see
/// <see cref="PaintLayer"/>.
/// </para>
/// </remarks>
static class PdfPainter
{
    /// <summary>
    /// Paints the slice of <paramref name="root"/> that <paramref name="start"/> begins, up to
    /// <paramref name="pageEnd"/>.
    /// </summary>
    /// <param name="surface">The page being drawn.</param>
    /// <param name="root">The laid-out tree.</param>
    /// <param name="start">
    /// Where this page's content starts, and the table headers re-drawn above it.
    /// </param>
    /// <param name="pageEnd">
    /// Where the next page's content starts, or <see cref="float.PositiveInfinity"/> on the last
    /// page.
    /// </param>
    /// <param name="content">The page's content box, in layout units.</param>
    /// <param name="paper">The whole page, in points. What the canvas background covers.</param>
    /// <param name="scale">Points per layout unit.</param>
    /// <param name="links">Where each fragment identifier resolves to, or null.</param>
    /// <param name="margins">
    /// The page's <c>@page</c> margin boxes, already laid out in the page's own coordinates, or null
    /// when the document declares none.
    /// </param>
    /// <remarks>
    /// <paramref name="pageEnd"/> is not the same as the bottom of the page box, and the
    /// difference is the whole reason it is a parameter. A line that straddles the page boundary
    /// is moved WHOLE to the next page by <see cref="Paginator"/>, so the last line on this page
    /// can end well short of the paper. Painting everything down to the paper's edge instead would
    /// draw that line here, clipped in half, and then draw it again in full overleaf.
    /// </remarks>
    public static void Paint(
        Surface surface,
        LayoutBox root,
        PageStart start,
        float pageEnd,
        Rect content,
        Size paper,
        float scale,
        LinkTargets? links = null,
        List<LayoutBox>? margins = null)
    {
        // The canvas, before anything else and outside the transform stack below, because it is
        // measured in page points rather than layout units and covers the margins as well as the
        // content.
        PaintCanvas(surface, root, paper);

        using var _ = surface.PushTransform(Matrix.Scale(scale, scale));

        PaintContent(surface, root, start, pageEnd, content, scale, links);

        // OUTSIDE the clip the content is painted through, which is the whole point of a margin
        // box: it sits in the page margin, where nothing in the document can reach. After the
        // content rather than before it, so a running header is never buried by a box that
        // overflows its way — it cannot, being clipped, but the ordering costs nothing and the
        // alternative would have to be reasoned about.
        PaintMargins(surface, margins, scale);
    }

    /// <summary>
    /// Paints the document's own slice of this page, clipped to the content box.
    /// </summary>
    static void PaintContent(
        Surface surface,
        LayoutBox root,
        PageStart start,
        float pageEnd,
        Rect content,
        float scale,
        LinkTargets? links)
    {
        var pageTop = start.Top;
        var reserved = start.Reserved;

        // How far the document is moved to put this page's slice at the page's content origin, and
        // SNAPPED to a whole pixel.
        //
        // Every rectangle this engine fills is already snapped, but in LAYOUT units — and that only
        // lands on a device pixel if the page's own offset is a whole number of them. It frequently
        // is not: a page begins at an unbreakable unit's top edge, and under `border-collapse` a
        // table row's top edge is half a rule below a whole pixel. Everything on such a page then
        // sits on a half pixel and is drawn antialiased down both sides of every edge, which is a
        // page's worth of soft edges from one fraction. Chromium starts a fragment on a whole
        // device pixel; this is that, applied in the one place a whole page passes through.
        //
        // It was invisible until a table with no header group broke across a page: a repeated
        // header's band is the table's top edge to the group's bottom, which carries the same half
        // pixel and cancels it — so `page/table_header` was already integral and said nothing.
        var shift = Snap(content.Y + reserved - pageTop);

        // Link annotations are queued and applied when the page closes, so they never see the
        // transform stack below and have to be given page coordinates directly. Everything else on
        // this page is painted in layout units through that stack, so the two coordinate spaces
        // coexist and only annotations use this one — through the same shift, or a link would sit
        // half a pixel off the text it names.
        var toPage = (Rect rect) => new Rect(
            (content.X + rect.X) * scale,
            (shift + rect.Y) * scale,
            rect.Width * scale,
            rect.Height * scale);

        // Content is clipped to the page box so a box straddling a break stops at the edge rather
        // than painting over the margin, and so the next page's slice starts clean.
        //
        // To the paper, deliberately, and not to the break: a box the break falls INSIDE is
        // fragmented, and a browser fills the rest of the page with that fragment rather than
        // stopping it where the last line did. `page/multi_page_flow` measures exactly that — its
        // fifth paragraph keeps its background to the bottom edge with a line moved overleaf. The
        // boxes that must not paint here are the ones moved whole, which `Backgrounds` culls by
        // their top edge.
        using var clipPath = PdfPath.Rectangle(
            Rectangle.FromSize(content.X, content.Y, content.Width, content.Height));
        using var _ = surface.PushClip(clipPath);

        // Shift the document so this page's slice lands at the page's content origin, below
        // whatever band the repeated headers take. One transform for the whole page beats
        // offsetting every coordinate at every call site.
        using var __ = surface.PushTransform(Matrix.Translate(content.X, shift));

        var slice = new PageSlice(
            pageTop,
            pageTop + content.Height - reserved,
            pageEnd,
            reserved,
            links,
            toPage);

        // A footer band is placed against `End`, which is infinite on the last page — and a page
        // holding no more of a table than its own end has no footer to repeat, so the two never
        // meet. Asserting it here rather than guarding for it keeps the arithmetic above honest.
        Debug.Assert(start.Footers.Count == 0 || !float.IsInfinity(pageEnd));

        // The page is the initial stacking context, so everything on it goes down in the order
        // Appendix E gives that one — which is not the order it was declared in. A positioned box
        // belongs to the stacking layer of the page rather than to the flow position of whichever
        // box happened to contain it, and painting one inside its declaring parent buries it under
        // any later sibling; the box it is anchored to is frequently an ancestor of that sibling,
        // so the burial is the normal case rather than a corner one.
        PaintStack(surface, root, slice, collects: true, repeats: start);
    }

    /// <summary>
    /// Paints the page's <c>@page</c> margin boxes — its running headers, footers and page number.
    /// </summary>
    /// <remarks>
    /// They are laid out in the page's OWN coordinates rather than the document's, so there is no
    /// translate here and nothing to undo: a box built by <see cref="PageMargins"/> already knows
    /// which strip of the margin it sits in. Its slice is its own extent, which culls nothing and
    /// is what lets the ordinary painting machinery draw it.
    /// </remarks>
    static void PaintMargins(Surface surface, List<LayoutBox>? margins, float scale)
    {
        if (margins is not {Count: > 0} boxes)
        {
            return;
        }

        var toPage = (Rect rect) => new Rect(
            rect.X * scale,
            rect.Y * scale,
            rect.Width * scale,
            rect.Height * scale);

        foreach (var box in boxes)
        {
            var slice = new PageSlice(
                box.BorderBox.Y,
                box.BorderBox.Bottom,
                float.PositiveInfinity,
                0,
                null,
                toPage);

            PaintStack(surface, box, slice, collects: true);
        }
    }

    /// <summary>
    /// Re-draws a continuation page's table headers and footers in the bands reserved for them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each group keeps the x it was laid out at — a browser puts a repeated header at the table's
    /// own left edge rather than at the page's — and is stacked past the one before it, which is
    /// what an outer table's group and a nested one's need.
    /// </para>
    /// <para>
    /// A header goes to the page's own origin and a footer to where the page's content ENDED,
    /// which is the whole of the difference between them: measured, Chromium draws the repeated
    /// footer immediately below the last row that fitted rather than flush with the paper, so the
    /// blank strip a short page leaves is BELOW the footer and not above it.
    /// </para>
    /// <para>
    /// The original box, drawn a second time through a translate. Copying the subtree instead
    /// would be a second thing to keep in step with layout, and the geometry the corpus compares
    /// would then have two answers for one element.
    /// </para>
    /// </remarks>
    static void PaintRepeats(
        Surface surface,
        List<RepeatedRows> groups,
        PageSlice page,
        float origin)
    {
        var stacked = 0f;

        foreach (var group in groups)
        {
            var band = group.Band;
            var dy = origin + stacked - band.Y;

            using var moved = surface.PushTransform(Matrix.Translate(0, dy));

            PaintStack(surface, group.Group, page.Repeated(dy, band.Y, band.Bottom), collects: true);
            PaintHeaderLines(surface, group.Table, band);

            stacked += band.Height;
        }
    }

    /// <summary>
    /// Paints one stacking context: <paramref name="box"/>'s own decoration, the contexts inside
    /// it that sit under its content, its in-flow layer, and then the contexts above.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CSS 2.1 Appendix E's steps as far as the feature set reaches them. Step 1 is the box's own
    /// background and border, step 2 the negative <c>z-index</c> contexts, steps 3 to 5 the layer
    /// (see <see cref="PaintLayer"/>), and steps 6 and 7 everything left, ordered by
    /// <see cref="Ordered"/>.
    /// </para>
    /// <para>
    /// Step 1 is lifted out of the layer walk here rather than left where every other box's
    /// background is painted, and that lifting is the whole reason this method exists: a negative
    /// context paints UNDER its parent's content and OVER its parent's background, and those two
    /// are one walk until the background is taken out of it. Fusing them is what would make
    /// <c>z-index: -1</c> paint over the background of the very box that declares it, where a
    /// browser hides it behind — the property's most quoted surprise, and the row
    /// <c>position/z_index</c> keeps for it.
    /// </para>
    /// <para>
    /// <paramref name="collects"/> is false for a positioned box that establishes no context of
    /// its own. Its positioned descendants were flattened onto the page by the walk that found it,
    /// so gathering them again here would paint every one of them a second time.
    /// </para>
    /// <para>
    /// <paramref name="repeats"/> is passed by the page and by nothing else. A repeated table
    /// header or footer is a copy of table CONTENT, so it belongs between steps 3–5 and step 7 —
    /// after every in-flow background and line on the page, and under everything positioned, which
    /// is where a fixed running header has to stay. Drawing it before the page instead puts it
    /// under the root element's own background, which every stylesheet that colours the page paints
    /// over the whole sheet; drawing it after puts it over a fixed box it should sit below.
    /// </para>
    /// </remarks>
    static void PaintStack(
        Surface surface,
        LayoutBox box,
        PageSlice page,
        bool collects,
        PageStart? repeats = null)
    {
        var contexts = Ordered(box, collects);

        // Sorted, so the negative ones are a prefix and one scan finds where they end.
        var above = 0;

        while (above < contexts.Count &&
               contexts[above].Style.StackingOrder < 0)
        {
            above++;
        }

        Decorate(surface, box, page);

        for (var i = 0; i < above; i++)
        {
            PaintContext(surface, contexts[i], page);
        }

        PaintLayer(surface, box, page, decorated: true);

        if (repeats is {} start)
        {
            PaintRepeats(surface, start.Headers, page, page.ToPageOrigin);

            // Where the page's content ended, in the document coordinates everything here is drawn
            // in. `End` is the next page's top, so the footer's band begins exactly where the last
            // row on this page left off.
            PaintRepeats(surface, start.Footers, page, page.End);
        }

        for (var i = above; i < contexts.Count; i++)
        {
            PaintContext(surface, contexts[i], page);
        }
    }

    /// <summary>
    /// The contexts <paramref name="box"/> paints, in the order Appendix E paints them.
    /// </summary>
    /// <remarks>
    /// A STABLE sort on <see cref="ComputedStyle.StackingOrder"/>, which is what settles steps 6
    /// and 7 between them: <c>auto</c> and <c>0</c> share step 6 and fall back to tree order,
    /// which is the order <see cref="Hoisted"/> already yields. An unstable sort would reorder
    /// them arbitrarily and be right on every arrangement where no two share a level.
    ///
    /// Empty when <paramref name="collects"/> is false, which is the case for a positioned box
    /// that establishes no context: what is inside it belongs to the walk that found it.
    /// </remarks>
    static List<LayoutBox> Ordered(LayoutBox box, bool collects)
    {
        if (collects)
        {
            return Hoisted(box)
                .OrderBy(_ => _.Style.StackingOrder)
                .ToList();
        }

        return [];
    }

    /// <summary>
    /// Paints one hoisted box, and anything that establishes a context inside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The recursion through <see cref="PaintStack"/> is what keeps a stacking context's contents
    /// INSIDE it. A positioned box is flattened to the page, which is Appendix E's rule and what
    /// <see cref="Hoisted"/> does; a box establishing a context is not, because everything under it
    /// has to be composited as one group before that group is faded, and because a
    /// <c>z-index</c> inside it is measured against its siblings rather than against the page —
    /// which is what stops a descendant at <c>z-index: 100</c> climbing over a context its parent
    /// sits below.
    /// </para>
    /// <para>
    /// The fade wraps both, so a positioned descendant of a faded box is inside the group.
    /// </para>
    /// </remarks>
    static void PaintContext(Surface surface, LayoutBox box, PageSlice page)
    {
        // A fixed box is anchored to the PAGE rather than to the document, so it is drawn on every
        // one. Everything below here is painted through a translate that has already subtracted
        // this page's top; adding it back lands the box at the same place on every sheet, and
        // makes the whole of the repetition one transform rather than a second walk.
        if (box.Style.RepeatsOnEveryPage)
        {
            var dy = page.ToPageOrigin;

            using var repeated = surface.PushTransform(Matrix.Translate(0, dy));
            using var faded = Fade(surface, box);

            // The window is the whole page's content box rather than what the slice has left of
            // it: a fixed box is laid out against the page, so one anchored to the bottom edge
            // sits below anything a reserved band leaves room for.
            PaintStack(surface, box, page.Repeated(dy, 0, page.PageHeight), collects: true);
            return;
        }

        using var _ = Fade(surface, box);

        PaintStack(surface, box, page, collects: box.Style.CreatesStackingContext);
    }

    /// <summary>
    /// The transparency group an <c>opacity</c> box paints into, or nothing.
    /// </summary>
    /// <remarks>
    /// Isolated as well as faded. Without the isolation the alpha is applied to each drawing
    /// operation as it goes down rather than to the finished group, so two overlapping fills of
    /// the same colour composite to a darker shade in the overlap — which is exactly the
    /// difference between <c>opacity</c> and an alpha on the colour, and what
    /// <c>block/opacity</c>'s second row measures.
    /// </remarks>
    static LayerScope Fade(Surface surface, LayoutBox box)
    {
        var style = box.Style;

        // The transform goes outermost, so everything the fade covers is drawn through it.
        Layer? transform = style.Transform is {} css
            ? surface.PushTransform(css.Resolve(box.BorderBox))
            : null;

        if (style.Opacity >= 1)
        {
            return new(transform);
        }

        var isolated = surface.PushIsolated();
        return new(surface.PushOpacity(style.Opacity), isolated, transform);
    }

    /// <summary>
    /// Every positioned box under <paramref name="box"/>, in document order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Relatively positioned boxes as well as absolute and fixed ones: <c>position: relative</c>
    /// leaves a box in flow, and out of Appendix E's steps 3 and 7 all the same. Includes those
    /// nested inside floats and inside other positioned boxes, which land after their ancestor by
    /// virtue of the walk order.
    /// </para>
    /// <para>
    /// One FLAT list rather than a nested one, which is Appendix E's own rule: a positioned
    /// descendant of a float, or of a positioned box that took no context of its own, belongs to
    /// the PARENT stacking context and is ordered against that context's other children rather
    /// than against its own ancestor. The one thing that stops the walk is a box that DOES
    /// establish a context, whose contents are that context's to order and to composite.
    /// </para>
    /// </remarks>
    static IEnumerable<LayoutBox> Hoisted(LayoutBox box)
    {
        foreach (var child in box.Children)
        {
            if (child.Style.IsPositioned || child.Style.CreatesStackingContext)
            {
                yield return child;

                // Not descended into when it establishes a context: whatever is positioned inside
                // it belongs to that context and is collected by the walk over it instead.
                if (child.Style.CreatesStackingContext)
                {
                    continue;
                }
            }

            foreach (var positioned in Hoisted(child))
            {
                yield return positioned;
            }
        }

        foreach (var floated in box.Floats)
        {
            foreach (var positioned in Hoisted(floated.Box))
            {
                yield return positioned;
            }
        }

        // An inline-block hangs off a line rather than off Children, and a positioned box declared
        // inside one belongs to this context all the same.
        foreach (var line in box.Lines)
        {
            foreach (var atomic in line.Boxes)
            {
                foreach (var positioned in Hoisted(atomic))
                {
                    yield return positioned;
                }
            }
        }

        // An absolute box hangs off the box that DECLARED it rather than off its children, so this
        // is the branch nearly every one of them arrives through — and it needs the same guard the
        // child walk has. Without it a context reached this way is collected AND descended into,
        // which paints everything positioned inside it twice: once inside the context where it
        // belongs, and once flattened onto the page where its own level is compared against the
        // page's rather than against its siblings'. `position/z_index`'s last two rows found it.
        foreach (var entry in box.Positioned)
        {
            yield return entry.Box;

            if (entry.Box.Style.CreatesStackingContext)
            {
                continue;
            }

            foreach (var positioned in Hoisted(entry.Box))
            {
                yield return positioned;
            }
        }
    }

    /// <summary>
    /// Fills the whole page with the background that propagates to the canvas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CSS 2.1 §14.2: the root element's background is not painted on the root box, it is handed
    /// to the canvas and covers the entire surface — margins, and every part of the page the root
    /// box does not reach. A document whose content stops halfway down still has a coloured page
    /// below it.
    /// </para>
    /// <para>
    /// When the root has no background of its own, <c>body</c>'s is taken instead and body then
    /// paints none. That transfer is what makes <c>body { background: … }</c> colour a whole page,
    /// which is how nearly every real document sets its background.
    /// </para>
    /// <para>
    /// Nothing in the corpus caught this for a long time, because <c>Inputs/reset.css</c> paints
    /// the root white and the page under it was white already. Acid1 found it in one render.
    /// </para>
    /// </remarks>
    static void PaintCanvas(Surface surface, LayoutBox root, Size page)
    {
        if (CanvasBackground(root) is not {} color)
        {
            return;
        }

        // The root box then paints this colour again over its own area, which is left alone: a
        // Color here is opaque by construction — Rgb, Gray and Cmyk, with no alpha — so the second
        // fill is provably identical to the first rather than merely close to it.
        surface.FillRectangle(Rectangle.FromSize(0, 0, page.Width, page.Height), color);
    }

    /// <summary>
    /// The colour the canvas takes, from the root or else from <c>body</c>.
    /// </summary>
    static Color? CanvasBackground(LayoutBox root) =>
        root.Style.BackgroundColor ?? Body(root)?.Style.BackgroundColor;

    /// <summary>
    /// The <c>body</c> box, whose background propagates when the root has none.
    /// </summary>
    static LayoutBox? Body(LayoutBox root) =>
        root.Children.FirstOrDefault(
            _ => _.Element?.LocalName.Equals("body", StringComparison.OrdinalIgnoreCase) == true);

    /// <summary>
    /// Paints <paramref name="root"/> and its in-flow subtree as one layer, in the order CSS 2.1
    /// Appendix E gives: every background and border first, then the floats, then all the inline
    /// content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The phases are GLOBAL over the layer rather than applied box by box, and the difference is
    /// the whole point of the method. Painting each box's background immediately before its own
    /// lines is right until something overflows, at which point a later sibling's background
    /// covers an earlier sibling's text — a browser puts the text on top, because every background
    /// in the layer goes down before any content does. `block/overflow_paint` measures it.
    /// </para>
    /// <para>
    /// Called again for each float and each positioned box, which Appendix E paints as though they
    /// established stacking contexts. Their positioned descendants do not come with them: those
    /// belong to the parent context, which is why <see cref="Hoisted"/> flattens the whole page
    /// into one list rather than nesting.
    /// </para>
    /// </remarks>
    static void PaintLayer(Surface surface, LayoutBox root, PageSlice page, bool decorated = false)
    {
        Backgrounds(surface, root, page, decorate: !decorated);

        foreach (var floated in Floats(root))
        {
            using var fade = Fade(surface, floated);
            PaintLayer(surface, floated, page);
        }

        Inlines(surface, root, page);
    }

    /// <summary>
    /// Appendix E steps 3 and 4: the backgrounds and borders of a layer's boxes, in tree order.
    /// </summary>
    /// <remarks>
    /// <paramref name="decorate"/> is false when the caller has already painted the top box's own
    /// decoration as step 1, which <see cref="PaintStack"/> does so that a negative <c>z-index</c>
    /// context can be painted between the two.
    /// </remarks>
    static void Backgrounds(Surface surface, LayoutBox box, PageSlice page, bool decorate = true)
    {
        if (page.Skip(box))
        {
            return;
        }

        if (decorate)
        {
            Decorate(surface, box, page);
        }

        // The clip covers the DESCENDANTS and not the box itself: `overflow` clips what overflows
        // a box, and the box's own border and background are drawn to its border edge, which is
        // outside the padding box this clips to.
        using var _ = Clip(surface, box);

        foreach (var child in InFlow(box))
        {
            Backgrounds(surface, child, page);
        }

        // AFTER the subtree, which is where CSS 2.1 Appendix E puts a collapsed table's rules:
        // below every background of the table's own elements comes the whole grid, in one pass.
        // Painting them with the table's own decoration instead puts them under every cell
        // background, and a cell's background reaches the middle of the line — so a header row
        // with a fill of its own erased the rule under it entirely, which is what
        // `page/table_header` found.
        if (box.Style.Visibility == VisibilityKind.Visible)
        {
            PaintCollapsedLines(surface, box, page);
        }
    }

    /// <summary>
    /// One box's own background, borders and outline.
    /// </summary>
    /// <remarks>
    /// A box beginning at or after the break was moved WHOLE to the next page and does not appear
    /// on this one at all — which is a different thing from a box the break falls inside, whose
    /// fragment here fills the rest of the page. Hence a test on the top edge rather than a clip
    /// at the break: the clip would truncate the straddling case too, and a browser paints that
    /// one down to the paper.
    ///
    /// Bounding only the lines is what left a table row moved overleaf with a sliver of its cell
    /// backgrounds stranded at the foot of the page before. `page/table_break` measures it.
    /// </remarks>
    static void Decorate(Surface surface, LayoutBox box, PageSlice page)
    {
        if (page.Skip(box) ||
            box.BorderBox.Y >= page.End ||
            box.Style.Visibility != VisibilityKind.Visible ||
            Suppressed(box))
        {
            return;
        }

        PaintBackground(surface, box);
        PaintBorders(surface, box);
        PaintOutline(surface, box);
    }

    /// <summary>
    /// Draws a collapsed table's grid lines, which belong to the table rather than to its cells.
    /// </summary>
    /// <remarks>
    /// One rectangle per line, drawn once. Letting the two boxes either side each paint their own
    /// half seams at any odd width — 3px gives two 1.5px halves meeting on a half pixel, which
    /// antialiases into a visible join down the middle of every line. <c>table/collapse</c> keeps a
    /// 3px table for exactly that reason.
    ///
    /// With the backgrounds and borders, not with the cells' content: a grid line is the table's
    /// own decoration, and a cell whose text overflows should sit over it the way it sits over any
    /// other background.
    /// </remarks>
    static void PaintCollapsedLines(Surface surface, LayoutBox box, PageSlice page)
    {
        if (box.CollapsedLines is not {Count: > 0} lines)
        {
            return;
        }

        foreach (var line in lines)
        {
            // Bounded by the page the way every other piece of decoration is. A grid line belongs
            // to the table rather than to a row, so the list spans the whole table and a line
            // outside this page's slice used to be drawn and then clipped away by the page box.
            // That stopped being the same thing once a continuation page could push its content
            // down: a line from the page BEFORE then lands inside the band a repeated header
            // fills, which is where it does not belong.
            if (line.Bounds.Bottom <= page.Top ||
                line.Bounds.Y >= page.End)
            {
                continue;
            }

            surface.FillRectangle(
                Rectangle.FromSize(
                    line.Bounds.X,
                    line.Bounds.Y,
                    line.Bounds.Width,
                    line.Bounds.Height),
                line.Color);
        }
    }

    /// <summary>
    /// The grid lines that belong to a repeated table header, drawn with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A collapsed table's rules belong to the TABLE, not to the boxes either side of them, so
    /// they are not in the header's own subtree and re-drawing the header alone leaves it as three
    /// unruled cells.
    /// </para>
    /// <para>
    /// Only the lines lying WITHIN the header's band. A vertical rule spans the table's whole
    /// height, so it fails that and is left to the table's own paint, which draws it at the right
    /// length — copied here it would run from the top of the page down past wherever the table
    /// ends on it. The tolerance is the line's own thickness, since a rule is centred ON the
    /// boundary and so hangs half its width above the header's top edge.
    /// </para>
    /// </remarks>
    static void PaintHeaderLines(Surface surface, LayoutBox table, Rect band)
    {
        if (table.CollapsedLines is not {Count: > 0} lines)
        {
            return;
        }

        foreach (var line in lines)
        {
            var thickness = Math.Min(line.Bounds.Width, line.Bounds.Height);

            if (line.Bounds.Y < band.Y - thickness ||
                line.Bounds.Bottom > band.Bottom + thickness)
            {
                continue;
            }

            surface.FillRectangle(
                Rectangle.FromSize(
                    line.Bounds.X,
                    line.Bounds.Y,
                    line.Bounds.Width,
                    line.Bounds.Height),
                line.Color);
        }
    }

    /// <summary>
    /// Draws the outline, which sits outside the border edge and takes no space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Its inner edge is the border box grown by <c>outline-offset</c>, and it reaches outward from
    /// there by its own width — measured: a 3px outline at an offset of 4 on a box starting at y=38
    /// paints rows 31 to 33, so the gap is the offset and the ink is entirely outside it.
    /// </para>
    /// <para>
    /// Drawn as one ring rather than four edges, for the reason a uniform border is: two
    /// antialiased edges meeting on a mitre diagonal do not composite to full coverage, and an
    /// outline is uniform by construction — it has one width and one colour on all four sides.
    /// </para>
    /// </remarks>
    static void PaintOutline(Surface surface, LayoutBox box)
    {
        var style = box.Style;

        if (style.OutlineWidth <= 0 || style.OutlineColor is not {} color)
        {
            return;
        }

        var inner = box.BorderBox.Deflate(
            -style.OutlineOffset,
            -style.OutlineOffset,
            -style.OutlineOffset,
            -style.OutlineOffset);

        var outer = inner.Deflate(
            -style.OutlineWidth,
            -style.OutlineWidth,
            -style.OutlineWidth,
            -style.OutlineWidth);

        using var builder = new PathBuilder();

        AddRectangle(builder, outer.X, outer.Y, outer.Right, outer.Bottom, clockwise: true);
        AddRectangle(builder, inner.X, inner.Y, inner.Right, inner.Bottom, clockwise: false);

        using var path = builder.Build();
        surface.SetFill(color).DrawPath(path);
    }

    /// <summary>
    /// Draws the backgrounds of the inline ELEMENTS this run sits inside.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An inline element generates no box, so there is nothing for the background phase to paint —
    /// its background belongs to step 7 with the inline content, one rectangle per line fragment.
    /// A single rectangle round the whole element would be wrong the moment it wrapped: it would
    /// colour the blank space at the end of one line and the indent at the start of the next.
    /// </para>
    /// <para>
    /// The rectangle is the run's baseline less the font's whole-pixel ascent, as tall as that
    /// ascent plus the whole-pixel descent — which is the same box the browser reports for the
    /// element and, measured, the same box it fills. Not the line box: two spans on a line with a
    /// generous <c>line-height</c> are backed to their text, with the leading left uncoloured.
    /// </para>
    /// <para>
    /// Ancestors first and innermost last, so a highlight nested inside another comes out on top.
    /// Each is measured against its OWN font rather than the run's, which is what backs a small
    /// nested element to the height of the large one containing it.
    /// </para>
    /// <para>
    /// Padding and border on an inline element are NOT drawn, and are reported instead. They are
    /// not a painting question: horizontal padding advances the text after it, so honouring them
    /// means changing where the words go.
    /// </para>
    /// </remarks>
    static void PaintInlineBackground(Surface surface, TextRun run)
    {
        if (run.Backdrops is {} backdrops)
        {
            foreach (var backdrop in backdrops)
            {
                Fill(surface, backdrop.Style, backdrop.Face, run);
            }
        }

        // A run with neither a selector nor a generated flag is the block's OWN text, whose
        // background the block already painted. Filling it again is invisible while the colour is
        // opaque and doubles the coverage the moment it is not.
        //
        // The flag is needed because generated content has no element and so no selector, and a
        // `::before { background: … }` painted nothing at all without it. Testing the style INSTANCE
        // against the block's instead — which would identify the block's own text by the same
        // reference identity `InlineLayout.InlineAlign` uses — is wrong for a run inside an
        // ANONYMOUS block: that block's style is a fresh instance while the text keeps its parent's,
        // so every anonymous run would paint its parent's background a second time.
        if (run.Selector is not null || run.Generated)
        {
            Fill(surface, run.Style, run.Face, run);
        }

        static void Fill(Surface surface, ComputedStyle style, FontFace face, TextRun run)
        {
            if (run.Width <= 0)
            {
                return;
            }

            // Snapped to whole pixels, because that is what the browser fills. A run starting at
            // 49.81 and ending at 127.21 is painted over columns 50 to 126 with hard edges rather
            // than with a fifth of a pixel of coverage at each end — and the rasteriser reading our
            // PDF snaps too, upward at both edges, so leaving the fractional rectangle in place
            // puts one whole extra column of colour at the right of every fragment. Rounding here
            // makes the two agree by construction instead of by luck.
            var left = Snap(run.X);
            var (top, bottom) = InlineMetrics.Extent(style, face, run.Y);

            var bounds = new Rect(left, top, Snap(run.X + run.Width) - left, bottom - top);

            PaintInlineSurface(surface, style, bounds, InlineEdgeKind.None);
        }
    }

    /// <summary>
    /// Paints one fragment of an inline element: its background, then the border edges this
    /// fragment owns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The top and bottom borders run the whole length of every fragment; the left is drawn only on
    /// the fragment holding the element's opening edge and the right only on its closing one. That
    /// is what makes a padded inline that wraps come out open at the break — a browser draws no
    /// vertical rule where a line ended, because the element did not end there.
    /// </para>
    /// <para>
    /// Four rectangles rather than the mitred path a block border takes. An inline border is a
    /// strip a couple of pixels thick around text, so the mitre is at most a pixel of a corner, and
    /// the fragment boundaries would break the path anyway.
    /// </para>
    /// </remarks>
    static void PaintInlineSurface(
        Surface surface,
        ComputedStyle style,
        Rect bounds,
        InlineEdgeKind kind)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        if (style.BackgroundColor is {} background)
        {
            using var path = PdfPath.Rectangle(
                Rectangle.FromSize(bounds.X, bounds.Y, bounds.Width, bounds.Height));

            using var inline = Krilla.Paint.Solid(background);
            surface.SetFill(new Fill(inline, style.BackgroundAlpha)).DrawPath(path);
        }

        Edge(style.BorderTopColor, style.BorderTop, bounds.X, bounds.Y, bounds.Width, style.BorderTop);

        Edge(
            style.BorderBottomColor,
            style.BorderBottom,
            bounds.X,
            bounds.Bottom - style.BorderBottom,
            bounds.Width,
            style.BorderBottom);

        if (kind == InlineEdgeKind.Leading)
        {
            Edge(style.BorderLeftColor, style.BorderLeft, bounds.X, bounds.Y, style.BorderLeft, bounds.Height);
        }

        if (kind == InlineEdgeKind.Trailing)
        {
            Edge(
                style.BorderRightColor,
                style.BorderRight,
                bounds.Right - style.BorderRight,
                bounds.Y,
                style.BorderRight,
                bounds.Height);
        }

        void Edge(Color? color, float width, float x, float y, float w, float h)
        {
            if (color is not {} painted || width <= 0 || w <= 0 || h <= 0)
            {
                return;
            }

            using var path = PdfPath.Rectangle(Rectangle.FromSize(x, y, w, h));
            surface.SetFill(painted).DrawPath(path);
        }
    }

    /// <summary>
    /// Rounds to the nearest whole pixel, halves upward.
    /// </summary>
    /// <remarks>
    /// Explicitly halves-up rather than through <see cref="MathF.Round(float)"/>, whose default is
    /// banker's rounding — which would send exactly one edge in a thousand the other way from the
    /// browser and leave a column of colour nobody could account for.
    /// </remarks>
    static float Snap(float value) =>
        MathF.Floor(value + 0.5f);

    /// <summary>
    /// The clip an <c>overflow</c> box imposes on its descendants, or nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pushed inside each PHASE rather than once around the box, which is what lets clipping
    /// coexist with Appendix E's global phase order. Each phase visits a box's subtree as one
    /// contiguous stretch of its walk, so a clip pushed for the duration of that stretch covers
    /// exactly the right boxes and nothing else — and the backgrounds of the whole page still go
    /// down before any of its content.
    /// </para>
    /// <para>
    /// The alternative, painting an <c>overflow</c> box's subtree as one unit under a single clip,
    /// is what a stacking context would do and this is not one. It would put the box's text down
    /// during the background phase, where a later sibling's background could cover it — which is
    /// the defect <c>block/overflow_paint</c> exists to catch.
    /// </para>
    /// <para>
    /// To the padding box, per CSS 2.1 §11.1.1, so a box with padding shows its content inside the
    /// padding and clips at the inner edge of its border.
    /// </para>
    /// </remarks>
    static LayerScope Clip(Surface surface, LayoutBox box)
    {
        var style = box.Style;

        if (style.Overflow == OverflowKind.Visible)
        {
            return default;
        }

        // The padding box, per CSS 2.1 section 11.1.1: the border box inset by the border alone,
        // so a box with padding shows its content inside that padding and clips at the inner edge
        // of the border rather than at the content edge.
        var padding = box.BorderBox.Deflate(
            style.BorderTop,
            style.BorderRight,
            style.BorderBottom,
            style.BorderLeft);

        using var path = PdfPath.Rectangle(
            Rectangle.FromSize(padding.X, padding.Y, padding.Width, padding.Height));

        return new(surface.PushClip(path));
    }

    /// <summary>
    /// Up to two graphics layers that may not be there, so a caller can <c>using</c> them
    /// unconditionally.
    /// </summary>
    /// <remarks>
    /// <see cref="Layer"/> is a struct, so a nullable one cannot be used in a <c>using</c>
    /// directly. Wrapping it beats the alternative of branching around each phase's whole walk,
    /// which would mean writing every recursion twice.
    ///
    /// Three of them because a faded, transformed box pushes a transform, an isolated group and an
    /// opacity, in that order, and releases them in the other.
    /// </remarks>
    readonly struct LayerScope(Layer? inner, Layer? middle = null, Layer? outer = null) :
        IDisposable
    {
        public void Dispose()
        {
            inner?.Dispose();
            middle?.Dispose();
            outer?.Dispose();
        }
    }

    /// <summary>
    /// Appendix E step 7: the content of a layer's boxes — lines, markers and replaced content —
    /// in tree order.
    /// </summary>
    /// <remarks>
    /// A block-level replaced element's content belongs here rather than with the backgrounds. It
    /// is content, and Appendix E paints it in the same step as the text around it, so an image
    /// hanging out of a short box sits over a later sibling for the same reason that box's text
    /// does.
    /// </remarks>
    static void Inlines(Surface surface, LayoutBox box, PageSlice page)
    {
        if (page.Skip(box))
        {
            return;
        }

        using var _ = Clip(surface, box);

        if (box.Image is {} replaced &&
            box.BorderBox.Y < page.End &&
            box.Style.Visibility == VisibilityKind.Visible)
        {
            PaintReplaced(surface, replaced, box.ContentBox, box.Style);
        }

        if (box.Style.Visibility == VisibilityKind.Visible)
        {
            PaintMarker(surface, box, page.Top, page.End);
        }

        foreach (var line in box.Lines)
        {
            // Bounded by where the next page starts, not by the paper. A line at or past the break
            // belongs overleaf and must not be drawn here even though it overlaps this sheet.
            if (line.Bounds.Bottom < page.Top || line.Bounds.Y >= page.End)
            {
                continue;
            }

            foreach (var run in line.Runs)
            {
                // Per RUN rather than per box, because `visibility` inherits and a descendant can
                // set it back to `visible` — so one line can hold hidden and visible text at once.
                // A link annotation is still queued for a hidden run: it carries no appearance, so
                // hiding the text does not hide the rectangle in a browser either.
                if (run.Style.Visibility == VisibilityKind.Visible)
                {
                    PaintInlineBackground(surface, run);

                    // Behind the text and in front of its background, which is where CSS puts a
                    // text shadow — over the element's own background and under its glyphs.
                    foreach (var shadow in run.Style.TextShadows)
                    {
                        PaintRun(
                            surface,
                            run with
                            {
                                X = run.X + shadow.OffsetX,
                                Y = run.Y + shadow.OffsetY,
                                Style = run.Style with
                                {
                                    Color = shadow.Color,
                                    TextShadows = [],
                                    Decorations = TextDecorations.None
                                }
                            });
                    }

                    PaintRun(surface, run);
                }


                PaintLink(surface, run, page.Links, page.ToPage);
            }

            // Ahead of the images and the atomic inlines, and after the runs, because an edge is
            // part of the same inline content step and nothing on a line overlaps it.
            foreach (var edge in line.Edges)
            {
                if (edge.Style.Visibility == VisibilityKind.Visible)
                {
                    PaintInlineSurface(
                        surface,
                        edge.Style,
                        edge.Bounds with {X = Snap(edge.Bounds.X), Width = Snap(edge.Bounds.Width)},
                        edge.Kind);
                }
            }

            foreach (var image in line.Images)
            {
                PaintImage(surface, image.Image, image.Bounds);
            }

            // Appendix E step 7.2.1: an inline-block paints as though it established a stacking
            // context, so its whole subtree goes down here as one unit rather than being spread
            // across this layer's phases. Its positioned descendants are the exception, and are
            // collected by `Hoisted` into the page's own list.
            foreach (var atomic in line.Boxes)
            {
                using var fade = Fade(surface, atomic);
                PaintLayer(surface, atomic, page);
            }
        }

        foreach (var child in InFlow(box))
        {
            Inlines(surface, child, page);
        }
    }

    /// <summary>
    /// The children a layer's own walks descend into: in flow, and not positioned.
    /// </summary>
    /// <remarks>
    /// A positioned child is in this list in the box tree — <c>position: relative</c> leaves a box
    /// in flow — and out of the layer for painting, because Appendix E's steps 3 and 7 are both
    /// about NON-positioned descendants. <see cref="Hoisted"/> collects it instead.
    /// </remarks>
    static IEnumerable<LayoutBox> InFlow(LayoutBox box) =>
        box.Children.Where(_ => _.Style is {IsPositioned: false, CreatesStackingContext: false});

    /// <summary>
    /// Every float in a layer, in tree order, gathered across the whole in-flow subtree rather
    /// than one box at a time.
    /// </summary>
    /// <remarks>
    /// Same reason the backgrounds are gathered globally: a float belongs to the layer, not to the
    /// box that declared it, so it goes down after every background in the layer and before every
    /// line. A float nested inside another one is reached by that float's own layer instead.
    ///
    /// Descendants before the box's own, which is the order the per-box walk this replaced
    /// produced. Two floats settle against each other only when one is too wide to fit and
    /// overflows the other, so no corpus scenario measures their relative order and the safe
    /// choice is the one that changes nothing.
    /// </remarks>
    static IEnumerable<LayoutBox> Floats(LayoutBox box)
    {
        foreach (var child in InFlow(box))
        {
            foreach (var floated in Floats(child))
            {
                yield return floated;
            }
        }

        foreach (var floated in box.Floats)
        {
            yield return floated.Box;
        }
    }

    /// <summary>
    /// The slice of the document one page shows, and what the painter needs to place a link on it.
    /// </summary>
    /// <param name="Top">Where this page's content starts, in layout units.</param>
    /// <param name="Bottom">The bottom of the paper, in layout units. Used only to cull.</param>
    /// <param name="End">
    /// Where the NEXT page's content starts, which is not the same as <paramref name="Bottom"/>:
    /// a unit straddling the boundary moves whole to the next page, so the last thing on this one
    /// can end well short of the sheet.
    /// </param>
    /// <param name="Reserved">
    /// The band at the top of the page that repeated table headers fill, and that the document
    /// slice is pushed below. Zero for nearly every page.
    /// </param>
    /// <param name="Links">Where each fragment identifier resolves to, or null.</param>
    /// <param name="ToPage">Layout units to page points, for annotations.</param>
    readonly record struct PageSlice(
        float Top,
        float Bottom,
        float End,
        float Reserved,
        LinkTargets? Links,
        Func<Rect, Rect> ToPage)
    {
        /// <summary>
        /// How far a box has to move to go from its position in the document to the same position
        /// on this page.
        /// </summary>
        /// <remarks>
        /// The page's transform stack has already subtracted <see cref="Top"/> and added
        /// <see cref="Reserved"/>, so undoing both is what puts a box at the page's own origin.
        /// Everything drawn per page rather than per document — a fixed box, a repeated header —
        /// starts from this.
        /// </remarks>
        public float ToPageOrigin => Top - Reserved;

        /// <summary>The height of the page's content box, the reserved band included.</summary>
        public float PageHeight => Bottom - Top + Reserved;

        /// <summary>
        /// Whether <paramref name="box"/>'s subtree can be skipped entirely for this page.
        /// </summary>
        /// <remarks>
        /// What keeps a long document's per-page cost proportional to what is on the page. Only
        /// leaves are culled: a box with a height smaller than its content lets that content
        /// overflow and paint outside the border box, so a parent that misses the page says
        /// nothing about where its children are.
        /// </remarks>
        public bool Skip(LayoutBox box) =>
            box.Children.Count == 0 &&
            box.Lines.Count == 0 &&
            box.Floats.Count == 0 &&
            (box.BorderBox.Bottom < Top || box.BorderBox.Y > Bottom);

        /// <summary>
        /// This slice as seen by content drawn at a page-relative position rather than a
        /// document-relative one.
        /// </summary>
        /// <param name="dy">
        /// The translate the caller pushed, which link annotations have to be offset by too —
        /// they never see the transform stack.
        /// </param>
        /// <param name="top">The top of the window such content is culled against.</param>
        /// <param name="bottom">Its bottom.</param>
        /// <remarks>
        /// <para>
        /// A repeated box — a fixed one, or a table header re-drawn at a continuation page's top —
        /// keeps the geometry layout gave it, which is a position on the one page it was laid out
        /// for. The caller paints it through a translate that puts it where this page wants it,
        /// and this moves the window to match: without it every such box is culled on every page
        /// but that one, which is the page nobody notices because it looks right.
        /// </para>
        /// <para>
        /// <see cref="End"/> goes to infinity for the same reason it exists at all — it culls a
        /// box that was moved WHOLE to a later page, and a box drawn again here was moved nowhere.
        /// </para>
        /// </remarks>
        public PageSlice Repeated(float dy, float top, float bottom)
        {
            // Copied out before the `with`, because a lambda inside a struct cannot capture
            // `this`.
            var toPage = ToPage;

            return this with
            {
                Top = top,
                Bottom = bottom,
                End = float.PositiveInfinity,
                Reserved = 0,
                ToPage = rect => toPage(rect.Offset(0, dy))
            };
        }
    }

    static void PaintBackground(Surface surface, LayoutBox box)
    {
        var style = box.Style;

        // `background-clip` decides where the whole background may be painted, and defaults to the
        // border box — which is why the strip under a border is painted at all.
        var rect = Area(box, style.BackgroundClip);

        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        // Shadows first of all, because a shadow is BEHIND the box that casts it — including
        // behind its own background, which is what makes a translucent background show the shadow
        // through where the two overlap.
        foreach (var shadow in style.BoxShadows)
        {
            PaintShadow(surface, style, box.BorderBox, shadow);
        }


        // The colour first and the image over it: they are two layers of one background rather
        // than alternatives, so a translucent gradient shows the colour through it.
        if (style.BackgroundColor is {} color)
        {
            Fill(Krilla.Paint.Solid(color), style.BackgroundAlpha);
        }

        if (style.BackgroundImage is {} gradient)
        {
            // Sized to the PADDING box, which is `background-origin: padding-box`, and painted out
            // to the border box below — so the two differ exactly when the box has a border, which
            // is also the only time the browser's tiling of the image becomes visible.
            Fill(GradientPaint.Create(
                gradient,
                Area(box, style.BackgroundOrigin),
                tiles: style.HasBorder));
        }

        if (style.BackgroundPicture is {} picture)
        {
            PaintBackgroundImage(surface, style, rect, Area(box, style.BackgroundOrigin), picture);
        }

        void Fill(Paint paint, float opacity = 1f)
        {
            using var owned = paint;
            var fill = new Fill(owned, opacity);

            if (!style.HasRadius)
            {
                // Snapped to whole pixels, because that is what the browser fills — a box from
                // 786.5 to 816.5 is painted over rows 787 to 816, not spread over 31 rows with half
                // coverage at each end. The same construction argument the inline background fill
                // and the background image both record; it shows here because `aspect-ratio` is the
                // first thing in the corpus that produces a fractional box height on purpose.
                //
                // Only the square path. A rounded one is a bezier outline whose corners would have
                // to be snapped with it, and no scenario asks for a fractional rounded box.
                var left = Snap(rect.X);
                var top = Snap(rect.Y);

                using var rectangle = PdfPath.Rectangle(
                    Rectangle.FromSize(
                        left,
                        top,
                        Snap(rect.Right) - left,
                        Snap(rect.Bottom) - top));

                surface.SetFill(fill).DrawPath(rectangle);
                return;
            }

            using var builder = new PathBuilder();
            RoundedBox.Resolve(style, rect).Trace(builder, rect, clockwise: true);

            using var path = builder.Build();
            surface.SetFill(fill).DrawPath(path);
        }
    }

    /// <summary>
    /// Tiles a raster background image across a box.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two rectangles are in play and they are not the same one. The image is POSITIONED against
    /// the padding box and PAINTED out to the border box, which is what makes the strip under a
    /// border carry the tail of the previous tile rather than the start of the first — the same
    /// asymmetry a gradient background has, reached from the other direction.
    /// </para>
    /// <para>
    /// A percentage position aligns that fraction of the IMAGE with the same fraction of the box,
    /// so <c>25%</c> on a 64px image in a 200px box puts the tile at 34px rather than at 50. That
    /// is measured, and it is why the position cannot go through the ordinary percentage path: it
    /// resolves against the room left over rather than against the box.
    /// </para>
    /// <para>
    /// The tile count is bounded. A background-size that resolves to a fraction of a pixel would
    /// otherwise ask for hundreds of thousands of draws, and a page that takes a minute to write is
    /// a worse answer than one whose pathological background stops early.
    /// </para>
    /// </remarks>
    static void PaintBackgroundImage(
        Surface surface,
        ComputedStyle style,
        Rect painted,
        Rect origin,
        ImageData image)
    {
        if (image.Width <= 0 || image.Height <= 0)
        {
            return;
        }

        var (width, height) = TileSize(style, origin, image);

        if (width <= 0 || height <= 0)
        {
            return;
        }

        // Snapped, because the browser snaps: `75%` of the 38px left over is 28.5, and Chrome
        // starts the tile on row 29 rather than straddling two rows at half coverage. The same
        // construction argument the inline background fill records.
        var x = Snap(Place(style.BackgroundPositionX, origin.Width, width) + origin.X);
        var y = Snap(Place(style.BackgroundPositionY, origin.Height, height) + origin.Y);

        // Back up to the last tile that still reaches into the painted area, so a repeated
        // background is continuous across the border rather than starting at the positioned tile.
        if (style.BackgroundRepeatX)
        {
            x -= MathF.Ceiling((x - painted.X) / width) * width;
        }

        if (style.BackgroundRepeatY)
        {
            y -= MathF.Ceiling((y - painted.Y) / height) * height;
        }

        var columns = style.BackgroundRepeatX
            ? (int) MathF.Ceiling((painted.Right - x) / width)
            : 1;

        var rows = style.BackgroundRepeatY
            ? (int) MathF.Ceiling((painted.Bottom - y) / height)
            : 1;

        const int most = 512;

        using var clipPath = PdfPath.Rectangle(
            Rectangle.FromSize(painted.X, painted.Y, painted.Width, painted.Height));
        using var clip = surface.PushClip(clipPath);

        for (var row = 0; row < Math.Min(rows, most); row++)
        {
            for (var column = 0; column < Math.Min(columns, most); column++)
            {
                PaintImage(
                    surface,
                    image,
                    new(x + column * width, y + row * height, width, height));
            }
        }
    }

    /// <summary>
    /// Whether <c>empty-cells: hide</c> keeps this box's background and border off the page.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It suppresses the INK and nothing else. The cell keeps its place in the grid and the rows do
    /// not close up, which is what makes the geometry comparison confirm the property by staying
    /// still — and it is the whole difference from <c>display: none</c>.
    /// </para>
    /// <para>
    /// Empty means no box and no line was generated, which is exactly what a cell holding only
    /// white space produces: collapsible white space generates no inline content, so the cell comes
    /// out indistinguishable from one written with nothing in it. Measured, and what Chrome does.
    /// </para>
    /// </remarks>
    static bool Suppressed(LayoutBox box) =>
        box is
        {
            Style:
            {
                Display: DisplayKind.TableCell,
                HideEmptyCells: true
            },
            Children.Count: 0,
            Lines.Count: 0,
            Floats.Count: 0,
            Positioned.Count: 0,
            Image: null
        };

    /// <summary>One of a box's three nested rectangles.</summary>
    static Rect Area(LayoutBox box, BoxArea area)
    {
        var style = box.Style;

        return area switch
        {
            BoxArea.Border => box.BorderBox,
            BoxArea.Padding => box.BorderBox.Deflate(
                style.BorderTop,
                style.BorderRight,
                style.BorderBottom,
                style.BorderLeft),
            _ => box.ContentBox
        };
    }

    /// <summary>
    /// How large one tile is, under <c>background-size</c>.
    /// </summary>
    /// <remarks>
    /// <c>auto</c> on one axis of an explicit pair keeps the image's proportions against whatever
    /// the other axis resolved to, which is the same rule a replaced element's auto dimension
    /// follows.
    /// </remarks>
    static (float Width, float Height) TileSize(ComputedStyle style, Rect origin, ImageData image)
    {
        var ratio = (float) image.Width / image.Height;

        switch (style.BackgroundSize)
        {
            case BackgroundSizing.Cover:
            case BackgroundSizing.Contain:
            {
                var horizontal = origin.Width / image.Width;
                var vertical = origin.Height / image.Height;

                var scale = style.BackgroundSize == BackgroundSizing.Cover
                    ? MathF.Max(horizontal, vertical)
                    : MathF.Min(horizontal, vertical);

                return (image.Width * scale, image.Height * scale);
            }

            case BackgroundSizing.Explicit:
            {
                var width = style.BackgroundSizeX.ResolveOrNull(origin.Width);
                var height = style.BackgroundSizeY.ResolveOrNull(origin.Height);

                return (
                    width ?? (height is {} h ? h * ratio : image.Width),
                    height ?? (width is {} w ? w / ratio : image.Height));
            }

            default:
                return (image.Width, image.Height);
        }
    }

    /// <summary>
    /// Where one axis of the first tile starts, relative to the positioning area.
    /// </summary>
    /// <remarks>
    /// A percentage resolves against the SLACK — the area less the tile — which is what makes
    /// <c>50%</c> centre the image and <c>100%</c> put its far edge on the area's far edge. An
    /// absolute length is an offset from the near edge, as it reads.
    /// </remarks>
    static float Place(CssLength position, float area, float tile)
    {
        if (position.Kind == LengthKind.Percent)
        {
            return (area - tile) * position.Value / 100f;
        }

        return position.Resolve(area);
    }

    /// <summary>
    /// Paints one box shadow: the border box, moved by the offset.
    /// </summary>
    /// <remarks>
    /// It keeps the box's corner radii, so a rounded box casts a rounded shadow — square corners
    /// under a rounded box show at every corner.
    /// </remarks>
    static void PaintShadow(Surface surface, ComputedStyle style, Rect border, BoxShadow shadow)
    {
        var rect = border.Offset(shadow.OffsetX, shadow.OffsetY);

        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        using var paint = Krilla.Paint.Solid(shadow.Color);
        var fill = new Fill(paint, shadow.Alpha);

        if (!style.HasRadius)
        {
            using var rectangle = PdfPath.Rectangle(
                Rectangle.FromSize(rect.X, rect.Y, rect.Width, rect.Height));

            surface.SetFill(fill).DrawPath(rectangle);
            return;
        }

        using var builder = new PathBuilder();
        RoundedBox.Resolve(style, rect).Trace(builder, rect, clockwise: true);

        using var path = builder.Build();
        surface.SetFill(fill).DrawPath(path);
    }

    /// <summary>
    /// Paints the four border edges, mitred at the corners.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each edge is a trapezium running from its two outer corners to its two inner ones, so
    /// adjacent edges meet along the diagonal between the outer corner and the padding box's
    /// corner rather than overlapping in a square. That diagonal is what CSS specifies and what a
    /// browser draws.
    /// </para>
    /// <para>
    /// The diagonal is invisible whenever the four edges share a colour, however their widths
    /// differ, which is exactly why it is worth being deliberate about: a border of one colour
    /// proves nothing about the corners. <c>block/borders</c> uses four different widths and four
    /// different colours for that reason. A border that does share one colour goes through
    /// <see cref="PaintUniformBorder"/> instead, and has to.
    /// </para>
    /// <para>
    /// The degenerate cases fall out rather than needing to be handled. An edge whose neighbours
    /// are zero wide has its inner corners directly below its outer ones, which is the rectangle
    /// the un-mitred version drew.
    /// </para>
    /// </remarks>
    static void PaintBorders(Surface surface, LayoutBox box)
    {
        var style = box.Style;
        if (!style.HasBorder)
        {
            return;
        }

        // Snapped at each edge, which everything below derives from — the ring, the mitred
        // trapezia and the patterned edges all measure from this rectangle, and snapping only the
        // inner one would leave a uniform border a fraction of a pixel wider on one side than the
        // other.
        var left = Snap(box.BorderBox.X);
        var top = Snap(box.BorderBox.Y);
        var outer = new Rect(left, top, Snap(box.BorderBox.Right) - left, Snap(box.BorderBox.Bottom) - top);

        // The padding box, which is where all four mitres converge. Clamped so that a border
        // thicker than the box it surrounds collapses to a degenerate inner rectangle rather than
        // an inside-out one.
        //
        // Snapped, the same way a block fill and a background image are, and for the same reason:
        // a browser puts a border edge on a pixel boundary, and a fractional one leaves a column of
        // half-covered pixels the reference does not have. Each EDGE is snapped rather than the
        // width, so a 3px border at x=10.5 stays 3px wide instead of becoming 2 or 4 depending on
        // where it fell — table columns make that case common, being fractional by nature.
        var innerLeft = Snap(Math.Min(outer.X + style.BorderLeft, outer.Right));
        var innerRight = Snap(Math.Max(outer.Right - style.BorderRight, innerLeft));
        var innerTop = Snap(Math.Min(outer.Y + style.BorderTop, outer.Bottom));
        var innerBottom = Snap(Math.Max(outer.Bottom - style.BorderBottom, innerTop));

        if (UniformColor(style) is {} uniform && style.PaintsBorderAsRing)
        {
            PaintUniformBorder(surface, style, uniform, outer, innerLeft, innerTop, innerRight, innerBottom);
            return;
        }

        // Every edge that is neither solid nor bevelled, drawn along its own centre line. A browser
        // does not mitre these into their neighbours — dashes and dots run past the corner and a
        // double border's two bands span the whole side — so the trapezium below, whose purpose is
        // to join two colours cleanly on a diagonal, is not what they want.
        PaintPatternedEdges(surface, style, outer);

        Edge(style.BorderTopStyle, style.BorderTop, style.BorderTopColor, Side.Top, style.BorderTopColorIsCurrent);
        Edge(style.BorderBottomStyle, style.BorderBottom, style.BorderBottomColor, Side.Bottom, style.BorderBottomColorIsCurrent);
        Edge(style.BorderLeftStyle, style.BorderLeft, style.BorderLeftColor, Side.Left, style.BorderLeftColorIsCurrent);
        Edge(style.BorderRightStyle, style.BorderRight, style.BorderRightColor, Side.Right, style.BorderRightColorIsCurrent);

        void Edge(BorderStyleKind kind, float width, Color? color, Side side, bool current)
        {
            if (width <= 0 || color is not {} declared || !Mitred(kind))
            {
                return;
            }

            // The top and left edges are the ones a bevel darkens under `inset`. Nothing else here
            // distinguishes them.
            var near = side is Side.Top or Side.Left;

            // One band for a solid or a single-shade bevel, two for a groove or a ridge — which is
            // Chromium's construction: the outer half of a groove is an `inset` edge and the inner
            // half an `outset` one.
            var bands = kind is BorderStyleKind.Groove or BorderStyleKind.Ridge ? 2 : 1;

            for (var band = 0; band < bands; band++)
            {
                Band(
                    Bevel.Shade(declared, kind, near, outer: band == 0, current),
                    (float) band / bands,
                    (float) (band + 1) / bands,
                    side);
            }
        }

        // The band between two rectangles nested inside the border box at `from` and `to` of the
        // way in — so 0 to 1 is the whole edge, and the two halves of a groove are 0 to 0.5 and
        // 0.5 to 1. All four sides step inward together, so the mitres still meet on the diagonal.
        void Band(Color color, float from, float to, Side side)
        {
            var (aLeft, aTop, aRight, aBottom) = Nested(from);
            var (bLeft, bTop, bRight, bBottom) = Nested(to);

            switch (side)
            {
                case Side.Top:
                    FillPolygon(
                        surface,
                        color,
                        new(aLeft, aTop),
                        new(aRight, aTop),
                        new(bRight, bTop),
                        new(bLeft, bTop));
                    return;
                case Side.Bottom:
                    FillPolygon(
                        surface,
                        color,
                        new(aRight, aBottom),
                        new(aLeft, aBottom),
                        new(bLeft, bBottom),
                        new(bRight, bBottom));
                    return;
                case Side.Left:
                    FillPolygon(
                        surface,
                        color,
                        new(aLeft, aBottom),
                        new(aLeft, aTop),
                        new(bLeft, bTop),
                        new(bLeft, bBottom));
                    return;
                default:
                    FillPolygon(
                        surface,
                        color,
                        new(aRight, aTop),
                        new(aRight, aBottom),
                        new(bRight, bBottom),
                        new(bRight, bTop));
                    return;
            }
        }

        // Clamped the same way the padding box above is, so a border thicker than the box it
        // surrounds collapses to a degenerate rectangle rather than an inside-out one.
        (float Left, float Top, float Right, float Bottom) Nested(float fraction)
        {
            var left = Snap(Math.Min(outer.X + style.BorderLeft * fraction, outer.Right));
            var top = Snap(Math.Min(outer.Y + style.BorderTop * fraction, outer.Bottom));

            return (
                left,
                top,
                Snap(Math.Max(outer.Right - style.BorderRight * fraction, left)),
                Snap(Math.Max(outer.Bottom - style.BorderBottom * fraction, top)));
        }
    }

    /// <summary>Which of the four border edges is being drawn.</summary>
    enum Side
    {
        Top,
        Right,
        Bottom,
        Left
    }

    /// <summary>
    /// Whether the style is drawn as a mitred trapezium rather than as a stroked line.
    /// </summary>
    /// <remarks>
    /// The two groups are exactly the ones whose ends have to meet a neighbour cleanly. A solid or
    /// bevelled edge does; a dash, a dot or a double band runs past the corner instead, which is
    /// what <see cref="PaintPatternedEdges"/> draws.
    /// </remarks>
    /// <summary>
    /// The widest dot Chromium draws as a crisp square rather than as an antialiased circle.
    /// </summary>
    /// <remarks>
    /// Measured at ten widths from 1 to 12 pixels. One, two and three come back with no
    /// antialiased pixel anywhere in them — a 3px dot is three solid pixels across — and four
    /// upward are genuine antialiased circles. Where between three and four the switch is made is
    /// not measurable, since a browser rounds a border's width to whole pixels first.
    /// </remarks>
    const float SquareDot = 3;

    static bool Mitred(BorderStyleKind kind) =>
        kind is BorderStyleKind.Solid
            or BorderStyleKind.Inset
            or BorderStyleKind.Outset
            or BorderStyleKind.Groove
            or BorderStyleKind.Ridge;

    /// <summary>
    /// Draws every border edge whose style is not solid.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The geometry was measured out of Chrome, which is the only way to get it: CSS says a dashed
    /// border is "a series of square-ended dashes" and leaves every length to the user agent. A
    /// dash is twice the border's width with a gap of its width, so the period is three times the
    /// width — an 8px border repeats every 24 pixels, 16 on and 8 off, and a 3px border every 9.
    /// </para>
    /// <para>
    /// A dot is the border's width across and repeats at twice it, and its SHAPE depends on how
    /// big it is: at or below <see cref="SquareDot"/> pixels Chromium draws a crisp square snapped
    /// to the pixel grid, and above it an antialiased circle. Both are drawn here, because neither
    /// approximates the other — a circle at 3px is a soft blob where the browser has three solid
    /// pixels, and a square at 12px is a visibly different shape.
    /// </para>
    /// <para>
    /// A double border is two bands each a third of the width with a third-width gap between them,
    /// which is why a 6px double reads 2-2-2 down a column of pixels and why <c>border: 1px
    /// double</c> is indistinguishable from solid.
    /// </para>
    /// <para>
    /// The period is then ADJUSTED so the side ends flush, which is what
    /// <see cref="FlushPattern"/> does: the dash keeps its length and the gap gives, because a side is
    /// almost never a whole number of periods long. Left unadjusted, every side ended on a partial
    /// dash and the corners disagreed with the browser by up to a whole period.
    /// </para>
    /// <para>
    /// A DOT is positioned by its centre rather than by its leading edge, since a zero-length dash
    /// under a round cap paints a circle around the point the pattern puts it at. So the line is
    /// pulled in half a width at each end — without that the first dot is centred on the corner and
    /// half of it falls outside the box.
    /// </para>
    /// </remarks>
    static void PaintPatternedEdges(Surface surface, ComputedStyle style, Rect outer)
    {
        Edge(style.BorderTopStyle, style.BorderTop, style.BorderTopColor, horizontal: true, near: true);
        Edge(style.BorderBottomStyle, style.BorderBottom, style.BorderBottomColor, horizontal: true, near: false);
        Edge(style.BorderLeftStyle, style.BorderLeft, style.BorderLeftColor, horizontal: false, near: true);
        Edge(style.BorderRightStyle, style.BorderRight, style.BorderRightColor, horizontal: false, near: false);

        void Edge(BorderStyleKind kind, float width, Color? color, bool horizontal, bool near)
        {
            if (width <= 0 || Mitred(kind) || color is not {} paint)
            {
                return;
            }

            if (kind == BorderStyleKind.Double)
            {
                // Outer band then inner, each a third of the width and each centred within its own
                // third — which puts a third-width gap between them.
                var band = width / 3;
                Line(band, band / 2, paint, null, round: false);
                Line(band, width - band / 2, paint, null, round: false);
                return;
            }

            // The whole side, corner to corner, which is what the pattern is fitted into. Dashes
            // run past the corner rather than mitring, so this is the border box's own extent.
            var side = horizontal ? outer.Width : outer.Height;

            if (kind == BorderStyleKind.Dashed)
            {
                var dash = width * 2;
                Line(width, width / 2, paint, [dash, FlushPattern(side, dash, width).Gap], round: false);
                return;
            }

            var dots = FlushPattern(side, width, width);

            // A SMALL dot is a snapped square rather than a circle, which is what Chromium draws
            // and is not something either shape can approximate: at three pixels it comes out of
            // the browser with no antialiased pixel anywhere in it.
            if (width <= SquareDot)
            {
                Squares(paint, dots.Count, width + dots.Gap);
                return;
            }

            // A dot is the border's width across and repeats at twice it, and the pattern places
            // its CENTRE — hence the inset of half a width at each end.
            Line(
                width,
                width / 2,
                paint,
                [0, width + dots.Gap],
                round: true,
                inset: width / 2);

            void Squares(Color line, int count, float pitch)
            {
                for (var index = 0; index < count; index++)
                {
                    // FLOORED, not rounded. Measured across a 200px side at three widths: the
                    // browser's dots land on exactly this sequence, and rounding moves half of
                    // them a pixel on.
                    var along = MathF.Floor(index * pitch);

                    var rect = horizontal
                        ? Rectangle.FromSize(
                            outer.X + along,
                            near ? outer.Y : outer.Bottom - width,
                            width,
                            width)
                        : Rectangle.FromSize(
                            near ? outer.X : outer.Right - width,
                            outer.Y + along,
                            width,
                            width);

                    using var path = PdfPath.Rectangle(rect);
                    surface.SetFill(line).DrawPath(path);
                }
            }

            void Line(float thickness, float offset, Color line, float[]? dashes, bool round, float inset = 0)
            {
                var along = near ? offset : -offset;

                using var builder = new PathBuilder();

                if (horizontal)
                {
                    var y = (near ? outer.Y : outer.Bottom) + along;
                    builder.MoveTo(outer.X + inset, y).LineTo(outer.Right - inset, y);
                }
                else
                {
                    var x = (near ? outer.X : outer.Right) + along;
                    builder.MoveTo(x, outer.Y + inset).LineTo(x, outer.Bottom - inset);
                }

                using var path = builder.Build();

                // The fill is cleared first: `DrawPath` applies whichever of the two are set, and
                // a stroked line left with a fill from the previous call would be filled as a
                // degenerate polygon as well.
                surface.SetFill(null);
                surface.SetStroke(line, thickness, round ? LineCap.Round : LineCap.Butt, dashes);
                surface.DrawPath(path);
                surface.SetStroke(null);
            }
        }
    }

    /// <summary>
    /// The colour all four edges share, or null when they do not all paint in one colour.
    /// </summary>
    /// <remarks>
    /// An edge that does not paint at all disqualifies the box, because the shortcut below draws a
    /// closed ring and a missing edge is a gap in it.
    /// </remarks>
    static Color? UniformColor(ComputedStyle style)
    {
        if (style.BorderTop <= 0 || style.BorderRight <= 0 ||
            style.BorderBottom <= 0 || style.BorderLeft <= 0)
        {
            return null;
        }

        if (style.BorderTopColor is {} color &&
            style.BorderRightColor == color &&
            style.BorderBottomColor == color &&
            style.BorderLeftColor == color)
        {
            return color;
        }

        return null;
    }

    /// <summary>
    /// Paints a border whose four edges share a colour, as one ring.
    /// </summary>
    /// <remarks>
    /// Not an optimisation — a correctness fix, and the reason a browser has the same special
    /// case. Four separate trapezia abut along the mitre diagonals, and two antialiased edges
    /// meeting on a diagonal do not composite to full coverage: each corner pixel comes out part
    /// transparent, leaving a visible nick in what should be a solid corner. Measured against
    /// Chrome it was about six pixels per corner. Drawn as one ring there are no internal edges to
    /// seam, which is why the corners come out exact.
    /// </remarks>
    static void PaintUniformBorder(
        Surface surface,
        ComputedStyle style,
        Color color,
        Rect outer,
        float innerLeft,
        float innerTop,
        float innerRight,
        float innerBottom)
    {
        using var builder = new PathBuilder();

        var radii = RoundedBox.Resolve(style, outer);
        var inner = new Rect(innerLeft, innerTop, innerRight - innerLeft, innerBottom - innerTop);

        if (radii.IsRounded)
        {
            radii.Trace(builder, outer, clockwise: true);
        }
        else
        {
            AddRectangle(builder, outer.X, outer.Y, outer.Right, outer.Bottom, clockwise: true);
        }

        // Wound the other way, so the non-zero rule cuts it out. Skipped when the border has
        // swallowed the box whole and there is nothing left to cut.
        if (innerRight > innerLeft && innerBottom > innerTop)
        {
            if (radii.IsRounded)
            {
                // Each inner radius is the outer one less the edge it runs along, floored at zero,
                // so a corner rounded less than its border is thick comes to a square inner corner.
                radii
                    .Deflate(style.BorderTop, style.BorderRight, style.BorderBottom, style.BorderLeft)
                    .Trace(builder, inner, clockwise: false);
            }
            else
            {
                AddRectangle(builder, innerLeft, innerTop, innerRight, innerBottom, clockwise: false);
            }
        }

        using var path = builder.Build();
        surface.SetFill(color).DrawPath(path);
    }

    /// <summary>Adds a rectangular contour wound in the given direction.</summary>
    /// <remarks>
    /// Built from segments rather than through <see cref="PathBuilder.AddRectangle"/> because the
    /// winding is the whole point here, and a rectangle primitive does not let the caller choose
    /// it.
    /// </remarks>
    static void AddRectangle(
        PathBuilder builder,
        float left,
        float top,
        float right,
        float bottom,
        bool clockwise)
    {
        builder.MoveTo(left, top);

        if (clockwise)
        {
            builder.LineTo(right, top);
            builder.LineTo(right, bottom);
            builder.LineTo(left, bottom);
        }
        else
        {
            builder.LineTo(left, bottom);
            builder.LineTo(right, bottom);
            builder.LineTo(right, top);
        }

        builder.Close();
    }

    /// <summary>Fills a closed polygon with a solid colour.</summary>
    /// <summary>
    /// How many dashes fit <paramref name="length"/> exactly, and the gap that makes them.
    /// </summary>
    /// <param name="length">The side's length, corner to corner.</param>
    /// <param name="dash">The dash's own length, which is held fixed.</param>
    /// <param name="wanted">The gap the pattern asks for, which is what the result is chosen near.</param>
    /// <remarks>
    /// <para>
    /// Chromium's rule, and it is the DASH that is held rather than the gap: two counts bracket the
    /// side — the most dashes that fit at the requested gap, and one more — and whichever leaves a
    /// gap closer to the one asked for wins. A 266px side under a 6px dash and a 3px gap takes 30
    /// dashes at 2.97 rather than 29 at 3.29, so the last dash ends on the corner.
    /// </para>
    /// <para>
    /// A side has one gap FEWER than it has dashes, because both ends carry a dash. Dividing by the
    /// dash count instead leaves the side ending on a gap, which is a whole period out at the far
    /// corner and looks like a phase error rather than a counting one.
    /// </para>
    /// </remarks>
    static (int Count, float Gap) FlushPattern(float length, float dash, float wanted)
    {
        if (length <= 0 || dash <= 0 || wanted <= 0)
        {
            return (0, wanted);
        }

        var fewer = MathF.Floor(length / (dash + wanted));

        if (fewer < 1)
        {
            return (1, wanted);
        }

        var narrow = Spread(fewer + 1);
        var wide = Spread(fewer);

        return MathF.Abs(narrow - wanted) < MathF.Abs(wide - wanted)
            ? ((int) fewer + 1, narrow)
            : ((int) fewer, wide);

        // One dash at each end and the rest shared out between them. A single dash has no gap to
        // give, so it keeps the requested one and loses the comparison above to the two-dash case.
        float Spread(float count) =>
            count <= 1 ? wanted : MathF.Max(0, (length - count * dash) / (count - 1));
    }

    static void FillPolygon(Surface surface, Color color, params ReadOnlySpan<Point> points)
    {
        using var path = PdfPath.Polygon(points);
        surface.SetFill(color).DrawPath(path);
    }

    /// <summary>
    /// Draws a list item's marker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Culled against where the NEXT page starts rather than against the paper, on the same
    /// reasoning as a line box: a marker belongs to the line it sits on, and drawing it on the
    /// sheet that line was moved off leaves a bullet with nothing beside it.
    /// </para>
    /// <para>
    /// A counter marker is a positioned run like any other text, so it goes through
    /// <see cref="PaintRun"/> and is drawn with the very glyphs it was measured with.
    /// </para>
    /// </remarks>
    static void PaintMarker(Surface surface, LayoutBox box, float top, float pageEnd)
    {
        if (box.Marker is not {} marker ||
            marker.Bounds.Bottom < top ||
            marker.Bounds.Y >= pageEnd)
        {
            return;
        }

        if (marker.Run is {} run)
        {
            PaintRun(surface, run);
            return;
        }

        var bounds = marker.Bounds;
        var color = box.Style.Color;

        if (marker.Kind == ListStyleKind.Square)
        {
            surface.FillRectangle(
                Rectangle.FromSize(bounds.X, bounds.Y, bounds.Width, bounds.Height),
                color);
            return;
        }

        var centreX = bounds.X + bounds.Width / 2;
        var centreY = bounds.Y + bounds.Height / 2;

        using var builder = new PathBuilder();
        AddCircle(builder, centreX, centreY, bounds.Width / 2);
        using var path = builder.Build();

        if (marker.Kind == ListStyleKind.Disc)
        {
            surface.SetFill(color).DrawPath(path);
            return;
        }

        // A hollow marker is the SAME circle, stroked one unit wide rather than filled — which is
        // why its ink reaches half a unit further out on every side than a disc of the same
        // nominal size. Stroking rather than filling a ring of two contours is not a stylistic
        // choice: it is what the browser puts in its PDF, and both sides of the corpus comparison
        // are rasterised by PDFium, so constructing the shape the same way is what makes the
        // pixels come out the same.
        //
        // The fill is cleared first because a path is drawn with whatever fill and stroke are
        // active, and the stroke afterwards so nothing later on the page is outlined.
        surface
            .SetFill(null)
            .SetStroke(color)
            .DrawPath(path)
            .SetStroke(null);
    }

    /// <summary>
    /// Adds a circular contour, as the four cubics every renderer approximates a circle with.
    /// </summary>
    /// <remarks>
    /// The control points sit <c>kappa</c> of the radius beyond each quadrant end, the constant
    /// that makes a cubic hug a quarter circle to within about one part in ten thousand — far
    /// below a pixel at any size a marker is drawn at.
    /// </remarks>
    static void AddCircle(PathBuilder builder, float x, float y, float radius)
    {
        const float kappa = 0.5522847498307936f;

        var pull = radius * kappa;

        builder.MoveTo(x + radius, y);
        builder.CubicTo(x + radius, y + pull, x + pull, y + radius, x, y + radius);
        builder.CubicTo(x - pull, y + radius, x - radius, y + pull, x - radius, y);
        builder.CubicTo(x - radius, y - pull, x - pull, y - radius, x, y - radius);
        builder.CubicTo(x + pull, y - radius, x + radius, y - pull, x + radius, y);
        builder.Close();
    }

    /// <summary>
    /// Draws a replaced element's content into its content box under <c>object-fit</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The four values differ only in what rectangle the image is drawn into, and every one of them
    /// was checked against Chrome with a 64x32 image in a 160x60 box: <c>fill</c> stretches to the
    /// whole box, <c>contain</c> gives 120x60 centred, <c>cover</c> gives 160x80 clipped, and
    /// <c>none</c> gives 64x32 centred. The centring is <c>object-position</c>'s initial value,
    /// which is the centre.
    /// </para>
    /// <para>
    /// A clip is pushed only where the content can reach outside the box, which is
    /// <c>cover</c> and an oversized <c>none</c>. It is not free — every clip is a graphics state
    /// push in the PDF — and <c>fill</c> and <c>contain</c> never need one.
    /// </para>
    /// </remarks>
    static void PaintReplaced(Surface surface, ImageData image, Rect content, ComputedStyle style)
    {
        var fit = style.ObjectFit;

        if (fit == ObjectFitKind.Fill || image.Width <= 0 || image.Height <= 0)
        {
            PaintImage(surface, image, content);
            return;
        }

        var horizontal = content.Width / image.Width;
        var vertical = content.Height / image.Height;

        var scale = fit switch
        {
            ObjectFitKind.Contain => MathF.Min(horizontal, vertical),
            ObjectFitKind.Cover => MathF.Max(horizontal, vertical),
            ObjectFitKind.ScaleDown => MathF.Min(1, MathF.Min(horizontal, vertical)),
            _ => 1f
        };

        var width = image.Width * scale;
        var height = image.Height * scale;

        var bounds = new Rect(
            content.X + Place(style.ObjectPositionX, content.Width, width),
            content.Y + Place(style.ObjectPositionY, content.Height, height),
            width,
            height);

        if (width <= content.Width && height <= content.Height)
        {
            PaintImage(surface, image, bounds);
            return;
        }

        using var clipPath = PdfPath.Rectangle(
            Rectangle.FromSize(content.X, content.Y, content.Width, content.Height));
        using var clip = surface.PushClip(clipPath);

        PaintImage(surface, image, bounds);
    }

    /// <summary>
    /// Draws an image into <paramref name="bounds"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// krilla stretches an image to whatever size it is given without preserving the aspect ratio,
    /// which is what lets the rectangle its caller computed decide the shape on its own.
    /// </para>
    /// <para>
    /// The one place the two kinds of image differ. Everything upstream — replaced sizing,
    /// <c>object-fit</c>, background tiling — has already reduced its question to a rectangle,
    /// and krilla-svg fills one the same way krilla's image drawing does.
    /// </para>
    /// </remarks>
    static void PaintImage(Surface surface, ImageData image, Rect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        // Snapped to whole pixels the way a block fill and a background image already are, and for
        // the same reason: the browser draws an image edge on a pixel boundary, and left fractional
        // the rasteriser reading the PDF picks up a column of half-covered pixels along the edge
        // that the reference does not have. Snapped at each EDGE rather than by rounding the size,
        // or a box at x=10.5 with width 63 comes out a pixel narrower than the same box at x=10.
        var left = Snap(bounds.X);
        var top = Snap(bounds.Y);

        var rectangle = Rectangle.FromSize(
            left,
            top,
            Snap(bounds.Right) - left,
            Snap(bounds.Bottom) - top);

        if (image.IsVector)
        {
            surface.DrawSvg(image.Svg, rectangle);
            return;
        }

        surface.DrawImage(image.Image, rectangle);
    }

    /// <summary>
    /// Adds a link annotation over <paramref name="run"/>, when it sits inside an anchor.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rectangle covers the run's em box — ascent above the baseline, descent below — rather
    /// than the whole line box. A line can be much taller than its text under a generous
    /// <c>line-height</c>, and a link that reaches into the blank space above and below its own
    /// words is clickable where nothing appears clickable.
    /// </para>
    /// <para>
    /// A <c>#fragment</c> becomes an internal link when the document actually has that id, and no
    /// annotation at all when it does not — a link that silently goes to the wrong page is worse
    /// than one that is absent.
    /// </para>
    /// </remarks>
    static void PaintLink(Surface surface, TextRun run, LinkTargets? links, Func<Rect, Rect> toPage)
    {
        if (run.Link is not {Length: > 0} href || run.Width <= 0)
        {
            return;
        }

        var ascent = run.Face.Ascent(run.Style.FontSize);
        var descent = run.Face.Descent(run.Style.FontSize);
        var area = toPage(new(run.X, run.Y - ascent, run.Width, ascent + descent));
        var bounds = Rectangle.FromSize(area.X, area.Y, area.Width, area.Height);

        if (!href.StartsWith('#'))
        {
            surface.AddLink(bounds, href);
            return;
        }

        if (links is not null && links.TryResolve(href[1..], out var page, out var target))
        {
            surface.AddLink(bounds, page, target);
        }
    }

    /// <summary>
    /// The thickness every text decoration is drawn at, in CSS pixels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>max(1, floor(size / 10))</c>, which does NOT read the font — measured out of Chrome across
    /// nineteen sizes from 10px to 60px and exact at every one. The font's own
    /// <c>post.underlineThickness</c> gives 0.8px at 16px, and both rules round to 1 there, which is
    /// why reading the face agreed with the browser for as long as the corpus only asked at 16px.
    /// They part company at 20px, where the font gives 1 and Chrome draws 2.
    /// </para>
    /// <para>
    /// The floor is the whole of it: 19px is one pixel thick and 20px is two, and no rounded
    /// expression reproduces that step. The same shape as the list-marker arithmetic, and for the
    /// same reason — CSS leaves the value to the user agent, so there is no correct number to
    /// compute and agreeing with the reference browser is the useful target.
    /// </para>
    /// </remarks>
    static float ResolvedThickness(float size) =>
        MathF.Max(1, MathF.Floor(size / 10));

    /// <summary>
    /// How far below the baseline an underline sits, in CSS pixels.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>ceil(size / 20)</c>, also measured rather than read from the face, and also exact across
    /// the same nineteen sizes: one pixel up to 20px, two from 21px to 40px, three from 44px. The
    /// CEILING is what puts the step at 21 rather than at 20, and it is what a font-derived position
    /// cannot produce — the offset is flat from 24px to 40px, where anything linear in the size
    /// would keep climbing.
    /// </para>
    /// <para>
    /// It is also held clear of the baseline by half the rule's own THICKNESS, which only shows once
    /// the thickness is overridden: a 4px rule on 20px text sits two pixels down rather than one,
    /// where the size alone would say one. Both halves are needed — the size term alone is a pixel
    /// short for a thick rule, and the thickness term alone is a pixel short at 44px.
    /// </para>
    /// </remarks>
    static float UnderlinePosition(float size, float thickness) =>
        MathF.Max(MathF.Ceiling(size / 20), MathF.Floor(thickness / 2));

    static void PaintRun(Surface surface, TextRun run)
    {
        if (run.Text.Length == 0)
        {
            return;
        }

        if (run.Glyphs is not {Count: > 0} glyphs)
        {
            return;
        }

        using var paint = Krilla.Paint.Solid(run.Style.Color);
        surface.SetFill(new Fill(paint, run.Style.TextAlpha));

        // The very glyphs the line was measured with, so what is painted is what was laid out.
        // Drawn from the baseline, which is where krilla positions a run's origin.
        surface.DrawGlyphs(
            new(run.X, run.Y),
            run.Face.Font,
            run.Style.FontSize,
            run.Text,
            glyphs);

        PaintDecorations(surface, run);
    }

    /// <summary>
    /// Draws whichever of the three rules <paramref name="run"/> asks for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every position and thickness comes from the font's own tables rather than from a fraction
    /// of the size, which is what puts the underline clear of the descenders in one face and tight
    /// under the baseline in another. Each is rounded to a whole pixel, and each was checked
    /// against Chrome's own render: at 16px Liberation Sans the underline lands one pixel below
    /// the baseline, the strike five above it and the overline fifteen above it, and all three
    /// agree.
    /// </para>
    /// <para>
    /// The overline is the one with no metric of its own. It sits on the top of the em box — the
    /// rounded ASCENT above the baseline — with the underline's thickness, since no table carries
    /// an overline geometry and that is where a browser puts it.
    /// </para>
    /// <para>
    /// All three take <see cref="ComputedStyle.DecorationColor"/>, which inherits alongside the
    /// decoration itself and falls back to the run's own colour when nobody named one. The
    /// remaining difference from a browser is narrower than it was and still there:
    /// <c>text/decorations</c> measures it — decorations are inherited here rather than propagated,
    /// so a nested span that names its own COLOUR and no decoration keeps the ancestor's rule and
    /// the ancestor's colour, which is right, while one that names its own <c>color</c> alone draws
    /// the rule in the child's colour where a browser keeps the declaring element's.
    /// </para>
    /// <para>
    /// The four rule styles are measured rather than derived, and none of the numbers is obvious.
    /// See <c>text/decoration_style</c>'s notes: a double rule is two lines separated by twice their
    /// thickness, and a patterned rule is drawn at TWICE the solid thickness, centred on it.
    /// </para>
    /// </remarks>
    static void PaintDecorations(Surface surface, TextRun run)
    {
        var decorations = run.Style.Decorations;

        if (decorations == TextDecorations.None)
        {
            return;
        }

        var size = run.Style.FontSize;
        var face = run.Face;

        // `text-decoration-thickness` replaces the resolved thickness for every one of the three
        // rules, and `text-underline-offset` moves only the underline — which is what the property
        // is named after and is the whole of its scope.
        var thickness = run.Style.DecorationThickness ?? ResolvedThickness(size);

        if (decorations.HasFlag(TextDecorations.Underline))
        {
            // A declared `text-underline-offset` REPLACES the resolved position rather than adding
            // to it. Measured: at 20px the resolved position is one pixel below the baseline, and
            // `text-underline-offset: 6px` puts the rule six below rather than seven. CSS describes
            // the property as an offset from the initial position, which reads as additive and is
            // not what the browser draws.
            Rule(
                run.Y + (run.Style.UnderlineOffset ?? UnderlinePosition(size, thickness)),
                thickness);
        }

        if (decorations.HasFlag(TextDecorations.Overline))
        {
            Rule(run.Y - MathF.Round(face.Ascent(size)) - thickness, thickness);
        }

        if (decorations.HasFlag(TextDecorations.LineThrough))
        {
            // Its POSITION is the font's — `OS/2.yStrikeoutPosition` — and its thickness is the
            // same resolved one the other two use. Only the position is a property of the face: a
            // strike has to cross the glyphs at the height that face was designed for, where a
            // thickness is the browser's choice.
            Rule(run.Y - face.StrikeoutOffset(size) - thickness, thickness);
        }

        void Rule(float top, float thickness)
        {
            var color = run.Style.DecorationColor ?? run.Style.Color;

            switch (run.Style.DecorationStyle)
            {
                case BorderStyleKind.Double:
                    // Two rules of the drawn thickness, separated by twice it — so a 1px underline
                    // at baseline+1 puts its second line at baseline+4. Measured; the arithmetic
                    // that puts them 2px apart is a rule thinner than the gap, which is not what
                    // Chrome draws.
                    surface.FillRectangle(Rectangle.FromSize(run.X, top, run.Width, thickness), color);
                    surface.FillRectangle(
                        Rectangle.FromSize(run.X, top + 3 * thickness, run.Width, thickness),
                        color);
                    return;

                case BorderStyleKind.Dashed:
                case BorderStyleKind.Dotted:
                    Patterned(top, thickness, color, run.Style.DecorationStyle);
                    return;

                default:
                    surface.FillRectangle(Rectangle.FromSize(run.X, top, run.Width, thickness), color);
                    return;
            }
        }

        // A patterned rule is drawn at TWICE the thickness a solid one gets, which is measured
        // rather than derived: Chrome's dashes are six pixels long with four-pixel gaps under a
        // 1px underline, and both numbers are multiples of two rather than of one. Blink's own
        // pattern is three widths on and two off for a dash, and one width on and one off for a
        // dot, which those numbers satisfy exactly at a width of two.
        void Patterned(float top, float thickness, Color color, BorderStyleKind kind)
        {
            var width = thickness * 2;
            var dash = kind == BorderStyleKind.Dashed ? width * 3 : width;
            var gap = kind == BorderStyleKind.Dashed ? width * 2 : width;

            // Centred on the solid rule's position rather than hanging below it, so the extra
            // thickness is taken half from each side. Floored onto a whole row, because the browser
            // is drawing on the pixel grid and a rule half a row down is two rows of grey where it
            // should be one of black.
            var band = MathF.Floor(top - (width - thickness) / 2);

            for (var x = run.X; x < run.X + run.Width; x += dash + gap)
            {
                var length = MathF.Min(dash, run.X + run.Width - x);
                surface.FillRectangle(Rectangle.FromSize(x, band, length, width), color);
            }
        }
    }
}
