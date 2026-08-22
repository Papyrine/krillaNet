/// <summary>
/// Sizes a replaced element — one whose content comes from outside CSS, which here means an image.
/// </summary>
/// <remarks>
/// <para>
/// Replaced elements size differently from everything else, and the difference is the aspect
/// ratio. An ordinary block with <c>width: auto</c> fills its container; an image with
/// <c>width: auto</c> uses its own intrinsic width. Specify one dimension and the other follows
/// from the ratio, which is why <c>width: 100%</c> on a photograph scales it rather than
/// stretching it.
/// </para>
/// <para>
/// CSS 2.1 §10.3.2 and §10.4. The min/max pass is the part most implementations get subtly wrong:
/// clamping the width has to rescale an auto height too, or a constrained image distorts.
/// </para>
/// </remarks>
static class ReplacedSizing
{
    /// <summary>
    /// The content size for <paramref name="image"/> under <paramref name="style"/>.
    /// </summary>
    /// <param name="style">The element's resolved style.</param>
    /// <param name="image">The image being sized.</param>
    /// <param name="available">
    /// The width available for content, after this element's own padding and border. Percentages
    /// resolve against it.
    /// </param>
    public static (float Width, float Height) Resolve(ComputedStyle style, ImageData image, float available)
    {
        var ratio = image.Ratio;
        var width = style.Width.ResolveOrNull(available);
        float? height = style.Height.Kind == LengthKind.Absolute ? style.Height.Value : null;

        switch (width, height)
        {
            case (not null, not null):
                break;

            // One dimension given: the other follows from the ratio, which is what keeps a scaled
            // image from distorting. With no ratio to follow, fall back to the intrinsic value.
            case (not null, null):
                height = ratio is {} r1 ? width.Value / r1 : image.Height;
                break;

            case (null, not null):
                width = ratio is {} r2 ? height.Value * r2 : image.Width;
                break;

            // Neither given: the image's own size, in CSS pixels one-for-one with its pixels.
            default:
                width = image.Width;
                height = image.Height;
                break;
        }

        return Clamp(style, width!.Value, height!.Value, available, ratio);
    }

    /// <summary>
    /// Applies <c>min-width</c> and <c>max-width</c>, carrying the height along.
    /// </summary>
    /// <remarks>
    /// The height is rescaled by whatever factor the width was clamped by, but ONLY when the
    /// height was not specified. An author who wrote both dimensions asked for that shape and gets
    /// it; an author who wrote only <c>max-width</c> gets a proportionally smaller image rather
    /// than a squashed one. Skipping this is how images end up distorted inside responsive
    /// containers.
    /// </remarks>
    static (float Width, float Height) Clamp(
        ComputedStyle style,
        float width,
        float height,
        float available,
        float? ratio)
    {
        var heightIsAuto = style.Height.Kind != LengthKind.Absolute;
        var used = width;

        if (style.MaxWidth.ResolveOrNull(available) is {} max)
        {
            used = Math.Min(used, max);
        }

        if (style.MinWidth.ResolveOrNull(available) is {} min)
        {
            used = Math.Max(used, min);
        }

        if (used == width || !heightIsAuto)
        {
            return (used, height);
        }

        return (used, ratio is {} value ? used / value : height);
    }
}
