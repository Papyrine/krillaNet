namespace Krilla.Html;

/// <summary>
/// How a document is converted: the page it goes onto, and the fonts it may use.
/// </summary>
/// <remarks>
/// Every dimension is in CSS pixels, at 96 to the inch, because that is the unit the document
/// itself is written in — a stylesheet saying <c>width: 600px</c> should not have to be reconciled
/// against a page expressed in points. The conversion to points happens once, when the page is
/// painted.
/// </remarks>
public sealed class HtmlOptions
{
    /// <summary>CSS pixels per point, for callers working in PDF units.</summary>
    public const float PixelsPerPoint = 96f / 72f;

    /// <summary>Page width in CSS pixels. Defaults to US Letter.</summary>
    public float PageWidth { get; set; } = 816;

    /// <summary>Page height in CSS pixels. Defaults to US Letter.</summary>
    public float PageHeight { get; set; } = 1056;

    /// <summary>Top page margin in CSS pixels.</summary>
    public float MarginTop { get; set; }

    /// <summary>Right page margin in CSS pixels.</summary>
    public float MarginRight { get; set; }

    /// <summary>Bottom page margin in CSS pixels.</summary>
    public float MarginBottom { get; set; }

    /// <summary>Left page margin in CSS pixels.</summary>
    public float MarginLeft { get; set; }

    /// <summary>
    /// The faces available to the document.
    /// </summary>
    /// <remarks>
    /// Required. krilla has no font database, so there is no set of fonts to fall back on — a
    /// conversion with none registered throws rather than silently producing a blank page.
    /// </remarks>
    public FontSet? Fonts { get; set; }

    /// <summary>
    /// The base address relative URLs resolve against.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// The root font size in CSS pixels, which <c>rem</c> resolves against and which an element
    /// without a <c>font-size</c> inherits. Browsers default to 16.
    /// </summary>
    public float RootFontSize { get; set; } = 16;

    /// <summary>Metadata written into the PDF.</summary>
    public DocumentMetadata? Metadata { get; set; }

    /// <summary>US Letter, 8.5 x 11 inches.</summary>
    public static HtmlOptions Letter => new();

    /// <summary>A4, 210 x 297 millimetres.</summary>
    public static HtmlOptions A4 =>
        new()
        {
            PageWidth = 210 / 25.4f * 96,
            PageHeight = 297 / 25.4f * 96
        };

    /// <summary>Sets all four page margins.</summary>
    public HtmlOptions WithMargin(float margin)
    {
        MarginTop = margin;
        MarginRight = margin;
        MarginBottom = margin;
        MarginLeft = margin;
        return this;
    }

    /// <summary>The width available to content, after page margins.</summary>
    internal float ContentWidth => Math.Max(0, PageWidth - MarginLeft - MarginRight);

    /// <summary>The height available to content, after page margins.</summary>
    internal float ContentHeight => Math.Max(0, PageHeight - MarginTop - MarginBottom);
}
