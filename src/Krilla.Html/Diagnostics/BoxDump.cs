namespace Krilla.Html.Diagnostics;

/// <summary>
/// One element's geometry, in the shape a browser's <c>getBoundingClientRect()</c> reports it.
/// </summary>
/// <param name="Selector">The element's selector path.</param>
/// <param name="X">Left edge of the border box, in CSS pixels, document-relative.</param>
/// <param name="Y">Top edge of the border box.</param>
/// <param name="Width">Border box width.</param>
/// <param name="Height">Border box height.</param>
public readonly record struct BoxGeometry(string Selector, float X, float Y, float Width, float Height);

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

        foreach (var box in root.Descendants())
        {
            if (box.Selector is {} selector)
            {
                boxes.Add(Geometry(selector, box.BorderBox));
            }

            foreach (var line in box.Lines)
            {
                foreach (var image in line.Images)
                {
                    if (image.Selector is {} imageSelector)
                    {
                        boxes.Add(Geometry(imageSelector, image.Bounds));
                    }
                }
            }
        }

        return boxes;
    }

    static BoxGeometry Geometry(string selector, Rect rect) =>
        new(selector, Round(rect.X), Round(rect.Y), Round(rect.Width), Round(rect.Height));

    /// <summary>
    /// Lays out <paramref name="html"/> and returns its element geometry, without producing a PDF.
    /// </summary>
    public static IReadOnlyList<BoxGeometry> Measure(string html, HtmlOptions options)
    {
        using var document = HtmlConverter.Parse(html, options);
        using var layout = HtmlConverter.LayoutDocument(document, options);
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
