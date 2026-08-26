// ReSharper disable RedundantUnsafeContext
namespace Krilla;

/// <summary>
/// The drawing area of a page. The origin is the top-left corner and Y increases downward.
/// </summary>
/// <remarks>
/// <para>
/// Obtained from <see cref="KrillaDocument.StartPage(Size)"/> and valid until that page is
/// closed. Drawing into a closed page throws rather than being silently misdirected at
/// whichever page is open at the time.
/// </para>
/// <para>
/// State is set then used: assign <see cref="SetFill(Fill?)"/> and/or <see cref="SetStroke(Stroke?)"/>, then
/// call <see cref="DrawPath"/>. Both can be active at once. With neither set, krilla fills
/// black rather than drawing nothing.
/// </para>
/// <para>
/// Transforms, clips and opacity use a push/pop stack. Every push needs a matching pop, and
/// the <see cref="Layer"/> helper makes that automatic.
/// </para>
/// </remarks>
public sealed partial class Surface
{
    readonly KrillaDocument document;
    readonly ulong token;

    internal Surface(KrillaDocument document, ulong token)
    {
        this.document = document;
        this.token = token;
    }

    IntPtr Handle => document.Handle;

    /// <summary>
    /// Sets the fill for subsequent drawing, or clears it when passed null.
    /// </summary>
    public Surface SetFill(Fill? fill)
    {
        var paint = fill?.Paint.Handle ?? IntPtr.Zero;
        var native = new NativeFill
        {
            Opacity = fill?.Opacity ?? 1f,
            Rule = (int) (fill?.Rule ?? FillRule.NonZero)
        };

        Status.Check(
            KrillaNative.krilla_surface_set_fill(Handle, token, paint, native),
            "Setting the fill");
        return this;
    }

    /// <summary>
    /// Sets the fill to a solid colour.
    /// </summary>
    /// <remarks>
    /// Allocates a paint that lives until the returned surface's document is disposed; for a
    /// colour used repeatedly, create one <see cref="Paint"/> and reuse it.
    /// </remarks>
    public Surface SetFill(Color color)
    {
        var paint = Paint.Solid(color);
        document.Track(paint);
        return SetFill(new Fill(paint));
    }

    /// <summary>
    /// Sets the stroke to a solid colour of the given width.
    /// </summary>
    /// <remarks>
    /// The counterpart to <see cref="SetFill(Color)"/>, and it allocates a paint on the same terms:
    /// it lives until the surface's document is disposed, so a colour stroked repeatedly is better
    /// served by one <see cref="Paint"/> reused.
    /// </remarks>
    public Surface SetStroke(Color color, float width = 1f)
    {
        var paint = Paint.Solid(color);
        document.Track(paint);
        return SetStroke(new Stroke(paint, width));
    }

    /// <summary>
    /// Sets a solid stroke of the given width, cap and dash pattern.
    /// </summary>
    /// <param name="color">Stroke colour.</param>
    /// <param name="width">Stroke width in surface units.</param>
    /// <param name="cap">How open ends — including each dash's ends — are drawn.</param>
    /// <param name="dashArray">Alternating on/off lengths, or null for a solid stroke.</param>
    /// <remarks>
    /// A zero-length dash under <see cref="LineCap.Round"/> draws a dot, which is how a dotted
    /// rule is made: <c>[0, 6]</c> puts a round dot every six units.
    /// </remarks>
    public Surface SetStroke(Color color, float width, LineCap cap, float[]? dashArray)
    {
        var paint = Paint.Solid(color);
        document.Track(paint);
        return SetStroke(new Stroke(paint, width, LineCap: cap, DashArray: dashArray));
    }

    /// <summary>
    /// Sets the stroke for subsequent drawing, or clears it when passed null.
    /// </summary>
    // ReSharper disable once RedundantUnsafeContext
    public unsafe Surface SetStroke(Stroke? stroke)
    {
        if (stroke is not { } value)
        {
            Status.Check(
                KrillaNative.krilla_surface_set_stroke(Handle, token, IntPtr.Zero, default),
                "Clearing the stroke");
            return this;
        }

        var dashes = value.DashArray ?? [];

        // Pinned only for the duration of the call: the native side copies the array into the
        // stroke it builds, so nothing outlives this frame.
        fixed (float* pinned = dashes)
        {
            var native = new NativeStroke
            {
                Width = value.Width,
                MiterLimit = value.MiterLimit,
                LineCap = (int) value.LineCap,
                LineJoin = (int) value.LineJoin,
                Opacity = value.Opacity,
                DashOffset = value.DashOffset,
                DashArray = (IntPtr) pinned,
                DashLength = (nuint) dashes.Length
            };

            Status.Check(
                KrillaNative.krilla_surface_set_stroke(Handle, token, value.Paint.Handle, native),
                "Setting the stroke");
        }

        return this;
    }

    /// <summary>
    /// Draws a path with the active fill and stroke.
    /// </summary>
    public Surface DrawPath(PdfPath path)
    {
        Status.Check(
            KrillaNative.krilla_surface_draw_path(Handle, token, path.Handle),
            "Drawing a path");
        return this;
    }

    /// <summary>
    /// Fills a rectangle with a solid colour.
    /// </summary>
    public Surface FillRectangle(Rectangle rectangle, Color color)
    {
        using var path = PdfPath.Rectangle(rectangle);
        return SetFill(color).DrawPath(path);
    }

    /// <summary>
    /// Draws a single line of text, shaping it with the bundled shaper.
    /// </summary>
    /// <param name="origin">The text baseline origin.</param>
    /// <param name="font">The font to draw with.</param>
    /// <param name="fontSize">Size in surface units.</param>
    /// <param name="text">The text to draw.</param>
    /// <param name="direction">Reading direction.</param>
    /// <param name="outlined">
    /// Draw glyphs as filled paths rather than as text. Makes them unselectable and
    /// unsearchable, but sidesteps font-embedding restrictions.
    /// </param>
    /// <remarks>
    /// Convenience with real limits: one script only, no bidirectional resolution, and no
    /// font fallback. Text needing any of those must be shaped by the caller and drawn with
    /// <see cref="DrawGlyphs"/>.
    /// </remarks>
    public Surface DrawText(
        Point origin,
        Font font,
        float fontSize,
        string text,
        TextDirection direction = TextDirection.Auto,
        bool outlined = false)
    {
        var utf8 = Encoding.UTF8.GetBytes(text);

        Status.Check(
            KrillaNative.krilla_surface_draw_text(
                Handle,
                token,
                origin.ToNative(),
                font.Handle,
                fontSize,
                utf8,
                (nuint) utf8.Length,
                outlined,
                (int) direction),
            "Drawing text");
        return this;
    }

    /// <summary>
    /// Draws a pre-shaped glyph run.
    /// </summary>
    /// <remarks>
    /// Glyph metrics are given in the font's design units and normalised here against
    /// <see cref="Font.UnitsPerEm"/>. Text offsets are given in UTF-16 code units, matching
    /// <see cref="string"/>, and translated to the UTF-8 byte offsets krilla expects.
    /// </remarks>
    public Surface DrawGlyphs(
        Point origin,
        Font font,
        float fontSize,
        string text,
        IReadOnlyList<Glyph> glyphs,
        bool outlined = false)
    {
        if (glyphs.Count == 0)
        {
            return this;
        }

        var utf8 = Encoding.UTF8.GetBytes(text);
        var offsets = Utf16ToUtf8Offsets(text);
        var scale = font.UnitsPerEm;
        var native = new NativeGlyph[glyphs.Count];

        for (var index = 0; index < glyphs.Count; index++)
        {
            var glyph = glyphs[index];
            var start = Math.Clamp(glyph.TextStart, 0, text.Length);
            var end = Math.Clamp(glyph.TextStart + glyph.TextLength, start, text.Length);

            native[index] = new()
            {
                GlyphId = glyph.GlyphId,
                TextStart = (uint) offsets[start],
                TextEnd = (uint) offsets[end],
                XAdvance = glyph.XAdvance / scale,
                XOffset = glyph.XOffset / scale,
                YOffset = glyph.YOffset / scale,
                YAdvance = glyph.YAdvance / scale,
                Location = 0
            };
        }

        Status.Check(
            KrillaNative.krilla_surface_draw_glyphs(
                Handle,
                token,
                origin.ToNative(),
                font.Handle,
                fontSize,
                utf8,
                (nuint) utf8.Length,
                native,
                (nuint) native.Length,
                outlined),
            "Drawing glyphs");
        return this;
    }

    /// <summary>
    /// Maps each UTF-16 index in <paramref name="text"/> to its UTF-8 byte offset.
    /// </summary>
    /// <remarks>
    /// krilla indexes the run text by UTF-8 byte range, and rejects an index that falls
    /// mid-character. Callers think in <see cref="string"/> indices, so the translation
    /// happens here rather than becoming their problem.
    /// </remarks>
    static int[] Utf16ToUtf8Offsets(string text)
    {
        var offsets = new int[text.Length + 1];
        var total = 0;

        for (var index = 0; index < text.Length; index++)
        {
            offsets[index] = total;
            var current = text[index];

            if (char.IsHighSurrogate(current) &&
                index + 1 < text.Length &&
                char.IsLowSurrogate(text[index + 1]))
            {
                // A surrogate pair is one 4-byte sequence. Both halves map to its start, so a
                // range that splits the pair still lands on a character boundary.
                offsets[index + 1] = total;
                index++;
                total += 4;
                continue;
            }

            total += current switch
            {
                < (char) 0x80 => 1,
                < (char) 0x800 => 2,
                _ => 3
            };
        }

        offsets[text.Length] = total;
        return offsets;
    }

    /// <summary>
    /// Draws an image scaled to fill the given size.
    /// </summary>
    /// <remarks>
    /// Aspect ratio is not preserved automatically; <see cref="PdfImage.Width"/> and
    /// <see cref="PdfImage.Height"/> supply the pixel dimensions needed to compute it.
    /// </remarks>
    public Surface DrawImage(PdfImage image, Rectangle bounds)
    {
        using (PushTransform(Matrix.Translate(bounds.Left, bounds.Top)))
        {
            Status.Check(
                KrillaNative.krilla_surface_draw_image(
                    Handle,
                    token,
                    image.Handle,
                    new Size(bounds.Width, bounds.Height).ToNative()),
                "Drawing an image");
        }

        return this;
    }

    /// <summary>
    /// Draws an SVG scaled to fill the given size.
    /// </summary>
    /// <param name="svg">The parsed document.</param>
    /// <param name="bounds">Where to draw it. The SVG is scaled to fill this and clipped to it.</param>
    /// <param name="embedText">
    /// Keeps text as selectable, searchable glyph runs. Turning it off outlines every glyph,
    /// which is larger and unsearchable, and is needed only where a font's licence forbids
    /// embedding.
    /// </param>
    /// <param name="filterScale">
    /// The resolution SVG filters are rasterised at, filters being the one part of SVG with no
    /// PDF equivalent. Higher is sharper and larger.
    /// </param>
    /// <remarks>
    /// Aspect ratio is not preserved automatically, exactly as for
    /// <see cref="DrawImage"/>; <see cref="PdfSvg.Width"/> and <see cref="PdfSvg.Height"/>
    /// supply the intrinsic size needed to compute it.
    /// </remarks>
    public Surface DrawSvg(
        PdfSvg svg,
        Rectangle bounds,
        bool embedText = true,
        float filterScale = 4)
    {
        using (PushTransform(Matrix.Translate(bounds.Left, bounds.Top)))
        {
            Status.Check(
                KrillaNative.krilla_surface_draw_svg(
                    Handle,
                    token,
                    svg.Handle,
                    new Size(bounds.Width, bounds.Height).ToNative(),
                    embedText,
                    filterScale),
                "Drawing an SVG");
        }

        return this;
    }

    /// <summary>
    /// Concatenates a transform, until the returned layer is disposed.
    /// </summary>
    public Layer PushTransform(Matrix transform)
    {
        Status.Check(
            KrillaNative.krilla_surface_push_transform(Handle, token, transform.ToNative()),
            "Pushing a transform");
        return new(this);
    }

    /// <summary>
    /// Intersects the drawing area with a clip path, until the returned layer is disposed.
    /// </summary>
    public Layer PushClip(PdfPath path, FillRule rule = FillRule.NonZero)
    {
        Status.Check(
            KrillaNative.krilla_surface_push_clip_path(Handle, token, path.Handle, (int) rule),
            "Pushing a clip path");
        return new(this);
    }

    /// <summary>
    /// Applies a base opacity, until the returned layer is disposed.
    /// </summary>
    /// <remarks>
    /// Nesting multiplies: two layers of 0.5 render at 0.25.
    /// </remarks>
    public Layer PushOpacity(float opacity)
    {
        Status.Check(
            KrillaNative.krilla_surface_push_opacity(Handle, token, opacity),
            "Pushing an opacity");
        return new(this);
    }

    /// <summary>
    /// Starts an isolated transparency group, until the returned layer is disposed.
    /// </summary>
    public Layer PushIsolated()
    {
        Status.Check(
            KrillaNative.krilla_surface_push_isolated(Handle, token),
            "Pushing an isolated layer");
        return new(this);
    }

    /// <summary>
    /// The current transformation matrix.
    /// </summary>
    public Matrix CurrentTransform
    {
        get
        {
            Status.Check(
                KrillaNative.krilla_surface_current_transform(Handle, token, out var transform),
                "Reading the current transform");
            return transform.ToManaged();
        }
    }

    /// <summary>
    /// Tags subsequent operations with a caller-chosen location, echoed back in validation
    /// errors so a failure can be traced to a place in the caller's own source document.
    /// Pass zero to clear.
    /// </summary>
    public Surface SetLocation(ulong location)
    {
        Status.Check(
            KrillaNative.krilla_surface_set_location(Handle, token, location),
            "Setting the location");
        return this;
    }

    internal void Pop() =>
        Status.Check(KrillaNative.krilla_surface_pop(Handle, token), "Popping a layer");
}
