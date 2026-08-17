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
/// Paint order follows CSS 2.1 Appendix E as far as the feature set goes: backgrounds, then
/// borders, then text, with each box's own decoration painted before its children's.
/// </para>
/// </remarks>
static class PdfPainter
{
    /// <summary>
    /// Paints the slice of <paramref name="root"/> between <paramref name="pageTop"/> and
    /// <paramref name="pageTop"/> plus <paramref name="pageHeight"/>.
    /// </summary>
    /// <param name="surface">The page being drawn.</param>
    /// <param name="root">The laid-out tree.</param>
    /// <param name="pageTop">Where this page's content starts, in layout units.</param>
    /// <param name="pageEnd">
    /// Where the next page's content starts, or <see cref="float.PositiveInfinity"/> on the last
    /// page.
    /// </param>
    /// <param name="content">The page's content box, in layout units.</param>
    /// <param name="scale">Points per layout unit.</param>
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
        float scale,
        LinkTargets? links = null)
    {
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
        using var clipPath = PdfPath.Rectangle(
            Rectangle.FromSize(content.X, content.Y, content.Width, content.Height));
        using var __ = surface.PushClip(clipPath);

        // Shift the document so this page's slice lands at the page's content origin. One
        // transform for the whole page beats offsetting every coordinate at every call site.
        using var ___ = surface.PushTransform(Matrix.Translate(content.X, content.Y - pageTop));

        PaintBox(surface, root, pageTop, pageTop + content.Height, pageEnd, links, toPage);
    }

    static void PaintBox(
        Surface surface,
        LayoutBox box,
        float top,
        float bottom,
        float pageEnd,
        LinkTargets? links,
        Func<Rect, Rect> toPage)
    {
        // Skipping off-page subtrees is what keeps a long document's per-page cost proportional to
        // what is actually on the page. Only leaves are culled: a box with an explicit height
        // smaller than its content lets that content overflow and paint outside the border box, so
        // a parent that misses the page says nothing about where its children are.
        if (box.Children.Count == 0 &&
            box.Lines.Count == 0 &&
            (box.BorderBox.Bottom < top || box.BorderBox.Y > bottom))
        {
            return;
        }

        PaintBackground(surface, box);
        PaintBorders(surface, box);

        // A block-level image paints into its content box, which replaced sizing already gave the
        // right aspect ratio — so no fitting is needed here.
        if (box.Image is {} replaced)
        {
            PaintImage(surface, replaced, box.ContentBox);
        }

        foreach (var line in box.Lines)
        {
            // Bounded by where the next page starts, not by the paper. A line at or past the break
            // belongs overleaf and must not be drawn here even though it overlaps this sheet.
            if (line.Bounds.Bottom < top || line.Bounds.Y >= pageEnd)
            {
                continue;
            }

            foreach (var run in line.Runs)
            {
                PaintRun(surface, run);
                PaintLink(surface, run, links, toPage);
            }

            foreach (var image in line.Images)
            {
                PaintImage(surface, image.Image, image.Bounds);
            }
        }

        foreach (var child in box.Children)
        {
            PaintBox(surface, child, top, bottom, pageEnd, links, toPage);
        }
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
    /// Paints the four border edges.
    /// </summary>
    /// <remarks>
    /// Each edge is a filled rectangle spanning the full border box on its axis, so adjacent edges
    /// overlap at the corners. Real CSS mitres them diagonally, which is visible only where two
    /// edges differ in colour or where a border is thick; for uniform borders the rendering is
    /// identical and the mitre is not worth the geometry.
    /// </remarks>
    static void PaintBorders(Surface surface, LayoutBox box)
    {
        var style = box.Style;
        if (!style.HasBorder)
        {
            return;
        }

        var rect = box.BorderBox;

        if (style.BorderTop > 0 && style.BorderTopColor is {} topColor)
        {
            surface.FillRectangle(
                Rectangle.FromSize(rect.X, rect.Y, rect.Width, style.BorderTop),
                topColor);
        }

        if (style.BorderBottom > 0 && style.BorderBottomColor is {} bottomColor)
        {
            surface.FillRectangle(
                Rectangle.FromSize(rect.X, rect.Bottom - style.BorderBottom, rect.Width, style.BorderBottom),
                bottomColor);
        }

        if (style.BorderLeft > 0 && style.BorderLeftColor is {} leftColor)
        {
            surface.FillRectangle(
                Rectangle.FromSize(rect.X, rect.Y, style.BorderLeft, rect.Height),
                leftColor);
        }

        if (style.BorderRight > 0 && style.BorderRightColor is {} rightColor)
        {
            surface.FillRectangle(
                Rectangle.FromSize(rect.Right - style.BorderRight, rect.Y, style.BorderRight, rect.Height),
                rightColor);
        }
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

        var shaped = TextMeasurer.Shape(run.Face, run.Text, run.Style.FontSize);
        if (shaped.Glyphs.Count == 0)
        {
            return;
        }

        surface.SetFill(run.Style.Color);

        // The same glyphs and advances the line was measured with, so what is painted is what was
        // laid out. Drawn from the baseline, which is where krilla positions a run's origin.
        surface.DrawGlyphs(
            new(run.X, run.Y),
            run.Face.Font,
            run.Style.FontSize,
            run.Text,
            shaped.Glyphs);

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
