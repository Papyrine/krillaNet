namespace Krilla;

/// <summary>
/// Page geometry and the optional PDF boundary boxes.
/// </summary>
/// <remarks>
/// Sizes are in points: 72 to the inch, so A4 is 595 x 842 and US Letter is 612 x 792.
/// </remarks>
public sealed class PageSettings
{
    /// <summary>
    /// Creates settings for a page of the given size.
    /// </summary>
    public PageSettings(Size size) =>
        Size = size;

    /// <summary>
    /// A4, 595 x 842 points.
    /// </summary>
    public static PageSettings A4 => new(new(595f, 842f));

    /// <summary>
    /// US Letter, 612 x 792 points.
    /// </summary>
    public static PageSettings Letter => new(new(612f, 792f));

    /// <summary>
    /// The size of the drawing surface.
    /// </summary>
    public Size Size { get; }

    /// <summary>
    /// The visible area. Defaults to the whole surface.
    /// </summary>
    public Rectangle? MediaBox { get; set; }

    /// <summary>
    /// The area to which the page is clipped when displayed or printed.
    /// </summary>
    public Rectangle? CropBox { get; set; }

    /// <summary>
    /// The area including bleed, for production printing.
    /// </summary>
    public Rectangle? BleedBox { get; set; }

    /// <summary>
    /// The intended finished size after trimming.
    /// </summary>
    public Rectangle? TrimBox { get; set; }

    /// <summary>
    /// The extent of meaningful content.
    /// </summary>
    public Rectangle? ArtBox { get; set; }

    internal NativePageSettings ToNative()
    {
        uint present = 0;

        if (MediaBox is not null)
        {
            present |= 1 << 0;
        }

        if (CropBox is not null)
        {
            present |= 1 << 1;
        }

        if (BleedBox is not null)
        {
            present |= 1 << 2;
        }

        if (TrimBox is not null)
        {
            present |= 1 << 3;
        }

        if (ArtBox is not null)
        {
            present |= 1 << 4;
        }

        return new()
        {
            Width = Size.Width,
            Height = Size.Height,
            MediaBox = (MediaBox ?? default).ToNative(),
            CropBox = (CropBox ?? default).ToNative(),
            BleedBox = (BleedBox ?? default).ToNative(),
            TrimBox = (TrimBox ?? default).ToNative(),
            ArtBox = (ArtBox ?? default).ToNative(),
            Present = present,
            Reserved = 0
        };
    }
}