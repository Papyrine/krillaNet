namespace Krilla.Html.Painting;

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
    /// Paints the slice of <paramref name="root"/> between <paramref name="pageTop"/> and
    /// <paramref name="pageEnd"/>.
    /// </summary>
    /// <param name="surface">The page being drawn.</param>
    /// <param name="root">The laid-out tree.</param>
    /// <param name="pageTop">Where this page's content starts, in layout units.</param>
    /// <param name="pageEnd">
    /// Where the next page's content starts, or <see cref="float.PositiveInfinity"/> on the last
    /// page.
    /// </param>
    /// <param name="content">The page's content box, in layout units.</param>
    /// <param name="page">The whole page, in points. What the canvas background covers.</param>
    /// <param name="scale">Points per layout unit.</param>
    /// <param name="links">Where each fragment identifier resolves to, or null.</param>
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
        float pageTop,
        float pageEnd,
        Rect content,
        Size page,
        float scale,
        LinkTargets? links = null)
    {
        // The canvas, before anything else and outside the transform stack below, because it is
        // measured in page points rather than layout units and covers the margins as well as the
        // content.
        PaintCanvas(surface, root, page);

        // Link annotations are queued and applied when the page closes, so they never see the
        // transform stack below and have to be given page coordinates directly. Everything else on
        // this page is painted in layout units through that stack, so the two coordinate spaces
        // coexist and only annotations use this one.
        var toPage = (Rect rect) => new Rect(
            (content.X + rect.X) * scale,
            (content.Y + rect.Y - pageTop) * scale,
            rect.Width * scale,
            rect.Height * scale);

        using var _ = surface.PushTransform(Matrix.Scale(scale, scale));

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
        using var __ = surface.PushClip(clipPath);

        // Shift the document so this page's slice lands at the page's content origin. One
        // transform for the whole page beats offsetting every coordinate at every call site.
        using var ___ = surface.PushTransform(Matrix.Translate(content.X, content.Y - pageTop));

        var slice = new PageSlice(pageTop, pageTop + content.Height, pageEnd, links, toPage);

        PaintLayer(surface, root, slice);

        // Positioned boxes are painted here rather than where they were declared, because they
        // belong to the stacking layer of the page and not to the flow position of whichever box
        // happened to contain them. Painting one inside its declaring parent buries it under any
        // later sibling — and the box it is anchored to is frequently an ancestor of that sibling,
        // so the burial is the normal case rather than a corner one.
        //
        // Flattened to one list rather than nested, which is Appendix E's own rule: a positioned
        // descendant of a float, or of another positioned box, belongs to the PARENT stacking
        // context and not to the one it sits inside.
        //
        // Tree order settles two of them, since nothing here establishes a stacking context and
        // z-index is not implemented.
        foreach (var positioned in Hoisted(root))
        {
            PaintLayer(surface, positioned, slice);
        }
    }

    /// <summary>
    /// Every positioned box under <paramref name="box"/>, in document order.
    /// </summary>
    /// <remarks>
    /// Relatively positioned boxes as well as absolute and fixed ones: <c>position: relative</c>
    /// leaves a box in flow, and out of Appendix E's steps 3 and 7 all the same. Includes those
    /// nested inside floats and inside other positioned boxes, which land after their ancestor by
    /// virtue of the walk order and so paint on top of it.
    /// </remarks>
    static IEnumerable<LayoutBox> Hoisted(LayoutBox box)
    {
        foreach (var child in box.Children)
        {
            if (child.Style.IsPositioned)
            {
                yield return child;
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

        foreach (var entry in box.Positioned)
        {
            yield return entry.Box;

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
    static void PaintLayer(Surface surface, LayoutBox root, PageSlice page)
    {
        Backgrounds(surface, root, page);

        foreach (var floated in Floats(root))
        {
            PaintLayer(surface, floated, page);
        }

        Inlines(surface, root, page);
    }

    /// <summary>
    /// Appendix E steps 3 and 4: the backgrounds and borders of a layer's boxes, in tree order.
    /// </summary>
    static void Backgrounds(Surface surface, LayoutBox box, PageSlice page)
    {
        if (page.Skip(box))
        {
            return;
        }

        // A box beginning at or after the break was moved WHOLE to the next page and does not
        // appear on this one at all — which is a different thing from a box the break falls
        // inside, whose fragment here fills the rest of the page. Hence a test on the top edge
        // rather than a clip at the break: the clip would truncate the straddling case too, and a
        // browser paints that one down to the paper.
        //
        // Bounding only the lines is what left a table row moved overleaf with a sliver of its
        // cell backgrounds stranded at the foot of the page before. `page/table_break` measures it.
        if (box.BorderBox.Y < page.End)
        {
            PaintBackground(surface, box);
            PaintBorders(surface, box);
        }

        foreach (var child in InFlow(box))
        {
            Backgrounds(surface, child, page);
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

        if (box.Image is {} replaced &&
            box.BorderBox.Y < page.End)
        {
            // Into its content box, which replaced sizing already gave the right aspect ratio, so
            // no fitting is needed here.
            PaintImage(surface, replaced, box.ContentBox);
        }

        PaintMarker(surface, box, page.Top, page.End);

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
                PaintRun(surface, run);
                PaintLink(surface, run, page.Links, page.ToPage);
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
        box.Children.Where(_ => !_.Style.IsPositioned);

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
    /// <param name="Links">Where each fragment identifier resolves to, or null.</param>
    /// <param name="ToPage">Layout units to page points, for annotations.</param>
    readonly record struct PageSlice(
        float Top,
        float Bottom,
        float End,
        LinkTargets? Links,
        Func<Rect, Rect> ToPage)
    {
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
    }

    static void PaintBackground(Surface surface, LayoutBox box)
    {
        if (box.Style.BackgroundColor is not {} color)
        {
            return;
        }

        var rect = box.BorderBox;
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        surface.FillRectangle(Rectangle.FromSize(rect.X, rect.Y, rect.Width, rect.Height), color);
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

        var outer = box.BorderBox;

        // The padding box, which is where all four mitres converge. Clamped so that a border
        // thicker than the box it surrounds collapses to a degenerate inner rectangle rather than
        // an inside-out one.
        var innerLeft = Math.Min(outer.X + style.BorderLeft, outer.Right);
        var innerRight = Math.Max(outer.Right - style.BorderRight, innerLeft);
        var innerTop = Math.Min(outer.Y + style.BorderTop, outer.Bottom);
        var innerBottom = Math.Max(outer.Bottom - style.BorderBottom, innerTop);

        if (UniformColor(style) is {} uniform)
        {
            PaintUniformBorder(surface, uniform, outer, innerLeft, innerTop, innerRight, innerBottom);
            return;
        }

        if (style is {BorderTop: > 0, BorderTopColor: {} topColor})
        {
            FillPolygon(
                surface,
                topColor,
                new(outer.X, outer.Y),
                new(outer.Right, outer.Y),
                new(innerRight, innerTop),
                new(innerLeft, innerTop));
        }

        if (style is {BorderBottom: > 0, BorderBottomColor: {} bottomColor})
        {
            FillPolygon(
                surface,
                bottomColor,
                new(outer.Right, outer.Bottom),
                new(outer.X, outer.Bottom),
                new(innerLeft, innerBottom),
                new(innerRight, innerBottom));
        }

        if (style is {BorderLeft: > 0, BorderLeftColor: {} leftColor})
        {
            FillPolygon(
                surface,
                leftColor,
                new(outer.X, outer.Bottom),
                new(outer.X, outer.Y),
                new(innerLeft, innerTop),
                new(innerLeft, innerBottom));
        }

        if (style is {BorderRight: > 0, BorderRightColor: {} rightColor})
        {
            FillPolygon(
                surface,
                rightColor,
                new(outer.Right, outer.Y),
                new(outer.Right, outer.Bottom),
                new(innerRight, innerBottom),
                new(innerRight, innerTop));
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

        return style.BorderTopColor is {} color &&
               style.BorderRightColor == color &&
               style.BorderBottomColor == color &&
               style.BorderLeftColor == color
            ? color
            : null;
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
        Color color,
        Rect outer,
        float innerLeft,
        float innerTop,
        float innerRight,
        float innerBottom)
    {
        using var builder = new PathBuilder();

        AddRectangle(builder, outer.X, outer.Y, outer.Right, outer.Bottom, clockwise: true);

        // Wound the other way, so the non-zero rule cuts it out. Skipped when the border has
        // swallowed the box whole and there is nothing left to cut.
        if (innerRight > innerLeft && innerBottom > innerTop)
        {
            AddRectangle(builder, innerLeft, innerTop, innerRight, innerBottom, clockwise: false);
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
    /// Draws an image into <paramref name="bounds"/>.
    /// </summary>
    /// <remarks>
    /// krilla stretches an image to whatever size it is given without preserving the aspect ratio,
    /// which is correct here: <see cref="ReplacedSizing"/> has already decided the shape, and
    /// deciding it twice is how a picture ends up subtly the wrong proportion.
    /// </remarks>
    static void PaintImage(Surface surface, ImageData image, Rect bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        surface.DrawImage(
            image.Image,
            Rectangle.FromSize(bounds.X, bounds.Y, bounds.Width, bounds.Height));
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

        surface.SetFill(run.Style.Color);

        // The very glyphs the line was measured with, so what is painted is what was laid out.
        // Drawn from the baseline, which is where krilla positions a run's origin.
        surface.DrawGlyphs(
            new(run.X, run.Y),
            run.Face.Font,
            run.Style.FontSize,
            run.Text,
            glyphs);

        if (run.Style.Underline)
        {
            // Position and thickness come from the font's own `post` table rather than a fixed
            // fraction of the size, which is what puts the rule clear of the descenders in one font
            // and tight under the baseline in another.
            surface.FillRectangle(
                Rectangle.FromSize(
                    run.X,
                    run.Y + run.Face.UnderlineOffset(run.Style.FontSize),
                    run.Width,
                    run.Face.UnderlineThickness(run.Style.FontSize)),
                run.Style.Color);
        }
    }
}
