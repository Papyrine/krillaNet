namespace Krilla.Web.Services;

/// <summary>The paper a conversion goes onto.</summary>
/// <remarks>
/// The names are the ones a print dialog uses, and the order is the one a print dialog puts them
/// in: the two defaults first, then the rest of the ISO A series, then the remaining North
/// American sizes.
/// </remarks>
public enum PaperSize
{
    Letter,
    A4,
    A3,
    A5,
    A6,
    B5,
    Legal,
    Tabloid,
    Executive
}

/// <summary>
/// One paper size, in the CSS pixels the engine measures everything in.
/// </summary>
/// <param name="Size">Which paper this is.</param>
/// <param name="Label">Its name and dimensions, for the picker.</param>
/// <param name="Width">Width in CSS pixels.</param>
/// <param name="Height">Height in CSS pixels.</param>
public readonly record struct Paper(PaperSize Size, string Label, float Width, float Height);

/// <summary>
/// The papers the app offers.
/// </summary>
/// <remarks>
/// <para>
/// Every size is written as the millimetres or inches it actually is and converted here, rather
/// than as the pixel figure that falls out. A4 is 210 x 297 mm; that it comes to 793.7 x 1122.5 CSS
/// pixels is an artefact of 96 to the inch and not something anyone should have to recognise in a
/// table — or, worse, round.
/// </para>
/// <para>
/// A4 and Letter reproduce <c>HtmlOptions</c>' own presets exactly, by the same arithmetic, so
/// routing them through this table changes nothing about what they produce.
/// </para>
/// </remarks>
public static class Papers
{
    const float PerInch = 96f;
    const float PerMillimetre = 96f / 25.4f;

    /// <summary>Every paper, in the order the picker shows them.</summary>
    public static IReadOnlyList<Paper> All { get; } =
    [
        Inches(PaperSize.Letter, "Letter", 8.5f, 11f),
        Millimetres(PaperSize.A4, "A4", 210, 297),
        Millimetres(PaperSize.A3, "A3", 297, 420),
        Millimetres(PaperSize.A5, "A5", 148, 210),
        Millimetres(PaperSize.A6, "A6", 105, 148),
        Millimetres(PaperSize.B5, "B5", 176, 250),
        Inches(PaperSize.Legal, "Legal", 8.5f, 14f),
        Inches(PaperSize.Tabloid, "Tabloid", 11f, 17f),
        Inches(PaperSize.Executive, "Executive", 7.25f, 10.5f)
    ];

    /// <summary>
    /// The named paper, falling back to Letter.
    /// </summary>
    /// <remarks>
    /// A fallback rather than a throw: the value arrives from a select element, and a page that
    /// refuses to convert because something handed it an unknown paper is a worse answer than one
    /// that converts onto the default.
    /// </remarks>
    public static Paper Find(PaperSize size)
    {
        foreach (var paper in All)
        {
            if (paper.Size == size)
            {
                return paper;
            }
        }

        return All[0];
    }

    static Paper Inches(PaperSize size, string name, float width, float height) =>
        new(size, $"{name} — {Trim(width)} × {Trim(height)} in", width * PerInch, height * PerInch);

    static Paper Millimetres(PaperSize size, string name, int width, int height) =>
        new(size, $"{name} — {width} × {height} mm", width * PerMillimetre, height * PerMillimetre);

    // 8.5 rather than 8.50, and 11 rather than 11.0.
    static string Trim(float value) =>
        value.ToString("0.##");
}
