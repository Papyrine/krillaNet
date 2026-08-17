/// <summary>
/// The body every corpus test shares: convert the scenario, render each page through PDFium,
/// compare both geometry and pixels to the browser reference, snapshot the lot, and regenerate the
/// scenario's <c>compare.md</c>.
/// </summary>
/// <remarks>
/// One test class per category because TUnit cannot filter on parameter values, so running just
/// one category's scenarios needs its own class. The classes carry nothing but their data source
/// and a one-line call to here.
/// </remarks>
static class CorpusRunner
{
    /// <summary>
    /// The faces every scenario is rendered with, loaded once for the whole run.
    /// </summary>
    /// <remarks>
    /// Shared because loading a face is comparatively expensive and using one is cheap, and
    /// because a single set is what guarantees every scenario resolves the same family to the same
    /// file. Never disposed: it lives for the process, and the run ends before it would matter.
    /// </remarks>
    static readonly Lazy<FontSet> fonts = new(LoadFonts);

    public static async Task Run(string directory)
    {
        var html = CorpusLayout.Html(directory);
        var options = Options();

        var pdf = HtmlConverter.Convert(html, options);
        var pages = RenderPages(pdf);

        var referencePages = CorpusLayout.ReferencePages(directory);

        var result = new CorpusResult
        {
            ReferencePageCount = referencePages.Length,
            ResultingPageCount = pages.Count,
            Boxes = CompareBoxes(directory, html, options),
            PageDiffs = ComparePages(referencePages, pages)
        };

        var targets = new List<Target>(pages.Count + 1)
        {
            new("pdf", new MemoryStream(pdf))
        };

        for (var index = 0; index < pages.Count; index++)
        {
            targets.Add(new("png", new MemoryStream(pages[index]), $"page_{index + 1:0000}"));
        }

        await Verify(result, targets)
            .UseDirectory(directory)
            .UseFileName(CorpusLayout.ResultName)
            .IgnoreParameters();

        CorpusMarkdownGenerator.Regenerate(directory);
    }

    /// <summary>The conversion settings the whole corpus shares.</summary>
    public static HtmlOptions Options() =>
        new()
        {
            PageWidth = CorpusLayout.PageWidth,
            PageHeight = CorpusLayout.PageHeight,
            Fonts = fonts.Value
        };

    /// <summary>
    /// Renders every page of <paramref name="pdf"/> to PNG.
    /// </summary>
    /// <remarks>
    /// Anti-aliasing is deliberately left ON, despite PDFium offering to switch it off. The point
    /// of comparison is a Chromium screenshot, and Chromium always antialiases — the launch flags
    /// the reference generator uses remove hinting and LCD subpixel rendering, not anti-aliasing
    /// itself. Disabling it here would make our render differ from the reference MORE, not less:
    /// every glyph edge and every fractional box edge would go from "smoothed slightly
    /// differently" to "smoothed versus not smoothed at all".
    /// </remarks>
    static List<byte[]> RenderPages(byte[] pdf)
    {
        using var document = PdfiumDocument.Load(pdf);

        var options = new RenderOptions
        {
            Dpi = CorpusLayout.Dpi
        };

        var pages = new List<byte[]>(document.PageCount);
        for (var index = 0; index < document.PageCount; index++)
        {
            pages.Add(document.RenderPage(index, options));
        }

        return pages;
    }

    /// <summary>
    /// Compares our element geometry to the browser's, or null when no reference has been
    /// generated for this scenario yet.
    /// </summary>
    static BoxComparisonResult? CompareBoxes(string directory, string html, HtmlOptions options)
    {
        var path = CorpusLayout.BoxesPath(directory);
        if (!File.Exists(path))
        {
            return null;
        }

        var reference = JsonSerializer.Deserialize(
                            File.ReadAllText(path),
                            CorpusJson.Default.ListBoxGeometry) ??
                        [];

        return BoxComparison.Compare(reference, BoxDump.Measure(html, options));
    }

    /// <summary>
    /// Per-page pixel comparison, suppressed entirely when the page counts disagree.
    /// </summary>
    static List<PageDiff>? ComparePages(string[] referencePages, List<byte[]> rendered)
    {
        if (referencePages.Length == 0 || referencePages.Length != rendered.Count)
        {
            return null;
        }

        var diffs = new List<PageDiff>(rendered.Count);

        for (var index = 0; index < rendered.Count; index++)
        {
            var referenceFile = referencePages[index];
            var (error, ssim) = PageComparison.Compare(referenceFile, rendered[index]);

            diffs.Add(new(
                index + 1,
                error,
                ssim,
                Path.GetFileName(referenceFile),
                $"{CorpusLayout.ResultName}#page_{index + 1:0000}.verified.png"));
        }

        return diffs;
    }

    static FontSet LoadFonts()
    {
        var set = new FontSet();
        set.AddDirectory(CorpusLayout.FontsDirectory);

        // The corpus stylesheets name the generic families rather than a specific face, so that a
        // scenario reads as CSS rather than as a font-loading exercise. These three bindings are
        // what make them resolve, and the reference generator maps the same three the same way.
        set.SansSerif = "Liberation Sans";
        set.Serif = "Liberation Serif";
        set.Monospace = "Liberation Mono";

        if (set.Fallback is null)
        {
            throw new InvalidOperationException(
                $"No fonts found in {CorpusLayout.FontsDirectory}. The corpus cannot be compared " +
                "to a browser without the faces both sides render with.");
        }

        return set;
    }
}

/// <summary>Serialization for the reference geometry files.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<BoxGeometry>))]
partial class CorpusJson : JsonSerializerContext;
