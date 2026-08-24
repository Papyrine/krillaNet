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
    /// <param name="containing">
    /// The containing block's content width, which a percentage resolves against. NOT the width
    /// left after this element's own padding and border: a percentage width on a replaced element
    /// is a share of its container, and subtracting the element's own surround first makes a
    /// padded image narrower than an unpadded one asking for the same share. Measured — Chrome
    /// gives a `width: 50%` image in a 600px block 300px of picture whatever padding it carries.
    /// </param>
    /// <param name="surroundX">
    /// The element's own horizontal padding and border, which <c>box-sizing: border-box</c> takes
    /// out of a declared width. The ratio then applies to what is LEFT of it: an image declared
    /// 100px wide with 15px of surround is 100px on the page and 70px of picture.
    /// </param>
    /// <param name="surroundY">
    /// The vertical pair, which the same rule takes out of a declared height. It has to be its own
    /// argument rather than reusing <paramref name="surroundX"/>: the two differ the moment the
    /// padding is not uniform, and a declared height deflated by the horizontal surround feeds a
    /// wrong number straight into the aspect ratio, so the WIDTH comes out wrong as well.
    /// </param>
    public static (float Width, float Height) Resolve(
        ComputedStyle style,
        ImageData image,
        float containing,
        float surroundX,
        float surroundY)
    {
        var ratio = image.Ratio;
        var width = style.ContentSize(style.Width.ResolveOrNull(containing), surroundX);
        float? height = style.Height.Kind == LengthKind.Absolute
            ? style.ContentSize(style.Height.Value, surroundY)
            : null;

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

            // Neither given, and the image has no size of its own: an SVG declaring only a
            // viewBox, whose root width and height default to 100%. The percentage resolves
            // against the containing block, and the height follows the ratio because the
            // containing block has no definite height for the second percentage to resolve
            // against. Measured — `image/svg`'s #viewbox row is 816 wide in Chrome and its
            // #inline row is 400, each the full content width of its own container, inline and
            // block-level alike.
            case (null, null) when !image.HasIntrinsicSize && ratio is {} r3:
                width = containing;
                height = containing / r3;
                break;

            // Neither given: the image's own size, in CSS pixels one-for-one with its pixels.
            default:
                width = image.Width;
                height = image.Height;
                break;
        }

        return Clamp(style, width!.Value, height!.Value, containing, surroundX, ratio);
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
        float containing,
        float surroundX,
        float? ratio)
    {
        var heightIsAuto = style.Height.Kind != LengthKind.Absolute;
        var used = width;

        if (style.ContentSize(style.MaxWidth.ResolveOrNull(containing), surroundX) is {} max)
        {
            used = Math.Min(used, max);
        }

        if (style.ContentSize(style.MinWidth.ResolveOrNull(containing), surroundX) is {} min)
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
