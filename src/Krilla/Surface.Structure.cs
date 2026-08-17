namespace Krilla;

public sealed partial class Surface
{
    /// <summary>
    /// Adds a link to an external URI over the given area.
    /// </summary>
    /// <remarks>
    /// Applied when the page closes, so it may be added at any point while the page is open.
    /// </remarks>
    public Surface AddLink(Rectangle bounds, string uri)
    {
        var utf8 = Encoding.UTF8.GetBytes(uri);

        Status.Check(
            KrillaNative.krilla_page_add_link(
                Handle,
                token,
                bounds.ToNative(),
                utf8,
                (nuint) utf8.Length,
                0,
                default),
            "Adding a link");
        return this;
    }

    /// <summary>
    /// Adds a link to a position within the document.
    /// </summary>
    /// <remarks>
    /// <paramref name="pageIndex"/> may name a page that does not exist yet.
    /// </remarks>
    public Surface AddLink(Rectangle bounds, int pageIndex, Point target = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);

        Status.Check(
            KrillaNative.krilla_page_add_link(
                Handle,
                token,
                bounds.ToNative(),
                [],
                0,
                (uint) pageIndex,
                target.ToNative()),
            "Adding a link");
        return this;
    }

    /// <summary>
    /// Adds an external link and returns an identifier for placing it in the tag tree.
    /// </summary>
    public TagIdentifier AddTaggedLink(Rectangle bounds, string uri)
    {
        var utf8 = Encoding.UTF8.GetBytes(uri);

        Status.Check(
            KrillaNative.krilla_page_add_tagged_link(
                Handle,
                token,
                bounds.ToNative(),
                utf8,
                (nuint) utf8.Length,
                0,
                default,
                out var identifier),
            "Adding a tagged link");
        return new(identifier);
    }

    /// <summary>
    /// Marks the start of a span of tagged text, to be closed with <see cref="EndTagged"/>.
    /// </summary>
    /// <param name="language">The span's natural language, as a BCP 47 tag.</param>
    /// <param name="altText">What a screen reader announces instead of the text.</param>
    /// <param name="expanded">The expansion of an abbreviation.</param>
    /// <param name="actualText">What the text says, when its glyphs do not spell it.</param>
    public TagIdentifier BeginText(
        string? language = null,
        string? altText = null,
        string? expanded = null,
        string? actualText = null) =>
        Begin(0, ArtifactKind.Other, null, language, altText, expanded, actualText);

    /// <summary>
    /// Marks the start of a span of non-text content — paths, images, or a mixture.
    /// </summary>
    public TagIdentifier BeginContent(string? altText = null) =>
        Begin(1, ArtifactKind.Other, null, null, altText, null, null);

    /// <summary>
    /// Marks the start of an artifact: content excluded from the logical structure, such as a
    /// running head or a decorative rule.
    /// </summary>
    /// <remarks>
    /// <see cref="ArtifactKind.Page"/> requires <paramref name="bounds"/>. The returned
    /// identifier is a placeholder and must not be placed in the tag tree — artifacts are, by
    /// definition, outside it.
    /// </remarks>
    public TagIdentifier BeginArtifact(ArtifactKind kind, Rectangle? bounds = null) =>
        Begin(2, kind, bounds, null, null, null, null);

    TagIdentifier Begin(
        int kind,
        ArtifactKind artifact,
        Rectangle? bounds,
        string? language,
        string? altText,
        string? expanded,
        string? actualText)
    {
        var languageUtf8 = Tag.Utf8(language);
        var altUtf8 = Tag.Utf8(altText);
        var expandedUtf8 = Tag.Utf8(expanded);
        var actualUtf8 = Tag.Utf8(actualText);

        Status.Check(
            KrillaNative.krilla_surface_start_tagged(
                Handle,
                token,
                kind,
                (int) artifact,
                (bounds ?? default).ToNative(),
                bounds is not null,
                languageUtf8,
                Tag.Length(language, languageUtf8),
                altUtf8,
                Tag.Length(altText, altUtf8),
                expandedUtf8,
                Tag.Length(expanded, expandedUtf8),
                actualUtf8,
                Tag.Length(actualText, actualUtf8),
                out var identifier),
            "Starting a tagged section");

        return new(identifier);
    }

    /// <summary>
    /// Ends the current tagged section.
    /// </summary>
    public Surface EndTagged()
    {
        Status.Check(
            KrillaNative.krilla_surface_end_tagged(Handle, token),
            "Ending a tagged section");
        return this;
    }

    /// <summary>
    /// Captures drawing operations into a reusable stream.
    /// </summary>
    /// <remarks>
    /// The callback draws onto a sub-surface rather than the page. Nothing appears on the page
    /// until the resulting stream is used — as a <see cref="Graphic"/>, a mask, or a pattern.
    /// </remarks>
    public ContentStream Capture(Action<Surface> draw)
    {
        Status.Check(
            KrillaNative.krilla_stream_begin(Handle, token, out var streamToken),
            "Opening a content stream");

        var sub = new Surface(document, streamToken);
        draw(sub);

        Status.Check(
            KrillaNative.krilla_stream_finish(Handle, streamToken, out var stream),
            "Closing a content stream");

        return new(stream);
    }

    /// <summary>
    /// Captures drawing operations as a reusable graphic.
    /// </summary>
    /// <param name="draw">Draws the graphic's content.</param>
    /// <param name="isolated">
    /// Give the graphic its own transparency group, so blending inside it does not interact
    /// with what is already on the page.
    /// </param>
    /// <remarks>
    /// Drawing the result repeatedly costs almost nothing: krilla emits the content once and
    /// references it.
    /// </remarks>
    public Graphic CaptureGraphic(Action<Surface> draw, bool isolated = false)
    {
        using var stream = Capture(draw);

        Status.Check(
            KrillaNative.krilla_graphic_new(Handle, stream.Handle, isolated, out var graphic),
            "Creating a graphic");
        return new(graphic);
    }

    /// <summary>
    /// Draws a previously captured graphic.
    /// </summary>
    public Surface DrawGraphic(Graphic graphic)
    {
        Status.Check(
            KrillaNative.krilla_surface_draw_graphic(Handle, token, graphic.Handle),
            "Drawing a graphic");
        return this;
    }

    /// <summary>
    /// Applies a soft mask, until the returned layer is disposed.
    /// </summary>
    /// <param name="draw">Draws the mask's content.</param>
    /// <param name="kind">How the mask's content becomes opacity.</param>
    public Layer PushMask(Action<Surface> draw, MaskType kind = MaskType.Luminosity)
    {
        using var stream = Capture(draw);

        Status.Check(
            KrillaNative.krilla_surface_push_mask(Handle, token, (int) kind, stream.Handle),
            "Pushing a mask");
        return new(this);
    }

    /// <summary>
    /// Builds a tiling pattern paint from captured drawing operations.
    /// </summary>
    /// <param name="draw">Draws one tile.</param>
    /// <param name="width">Tile width.</param>
    /// <param name="height">Tile height.</param>
    /// <param name="transform">A transform applied to the pattern space.</param>
    public Paint CapturePattern(
        Action<Surface> draw,
        float width,
        float height,
        Matrix? transform = null)
    {
        using var stream = Capture(draw);

        Status.Check(
            KrillaNative.krilla_paint_new_pattern(
                Handle,
                stream.Handle,
                (transform ?? Matrix.Identity).ToNative(),
                width,
                height,
                out var paint),
            "Creating a pattern");
        return Paint.FromHandle(paint);
    }

    /// <summary>
    /// Draws one page of an existing PDF into the given area.
    /// </summary>
    public Surface DrawPdfPage(PdfSource source, int pageIndex, Rectangle bounds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);

        using (PushTransform(Matrix.Translate(bounds.Left, bounds.Top)))
        {
            Status.Check(
                KrillaNative.krilla_surface_draw_pdf_page(
                    Handle,
                    token,
                    source.Handle,
                    new Size(bounds.Width, bounds.Height).ToNative(),
                    (nuint) pageIndex),
                "Drawing a PDF page");
        }

        return this;
    }
}
