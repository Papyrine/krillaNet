/// <summary>
/// The geometry every producer of a comparable image has to agree on, and the corpus layout on
/// disk.
///
/// COMPILED INTO BOTH Krilla.Html.Tests AND Krilla.Html.RefGen, by a linked &lt;Compile&gt; in
/// RefGen's project file. That is deliberate rather than tidy: the numbers below are shared
/// between the browser that produces a reference and the test that compares against it, and a
/// disagreement does not fail loudly. It silently suppresses SSIM and skews the error metric,
/// because the two images are no longer the same size — so the comparison keeps running and keeps
/// reporting numbers that mean nothing. One definition removes the possibility.
/// </summary>
static class CorpusLayout
{
    /// <summary>
    /// Page width in CSS pixels: US Letter, 8.5in at 96 per inch.
    /// </summary>
    /// <remarks>
    /// Also the browser viewport width. The corpus uses zero page margins so the page content box
    /// and the viewport are the same rectangle, which is what lets the root element's box be
    /// compared directly.
    /// </remarks>
    public const int PageWidth = 816;

    /// <summary>Page height in CSS pixels: US Letter, 11in at 96 per inch.</summary>
    public const int PageHeight = 1056;

    /// <summary>
    /// The DPI pages are rasterised at.
    /// </summary>
    /// <remarks>
    /// 96 is not a free choice. A CSS pixel is 1/96 inch, so rendering at 96 makes one device
    /// pixel one CSS pixel, and the PDF raster comes out at exactly
    /// <see cref="PageWidth"/> x <see cref="PageHeight"/> — the same dimensions as the browser
    /// screenshot. Any other value would need the screenshot scaled to match, and a resample
    /// would put blur into the very comparison it is there to inform.
    /// </remarks>
    public const int Dpi = 96;

    /// <summary>The name a scenario's HTML is stored under.</summary>
    public const string HtmlFile = "input.html";

    /// <summary>The name a scenario's CSS is stored under.</summary>
    public const string CssFile = "input.css";

    /// <summary>The stylesheet every scenario includes, shared across the corpus.</summary>
    public const string ResetFile = "reset.css";

    /// <summary>The file holding the browser's element geometry.</summary>
    public const string BoxesFile = "reference.boxes.json";

    /// <summary>
    /// The prefix of a reference page render.
    /// </summary>
    /// <remarks>
    /// Named "reference", not "expected". We do not expect to match it: two rasterisers disagree
    /// about glyph edges however correct the layout is, so equality is not the goal and a name
    /// promising it would misdescribe every comparison in the suite. "Reference rendering" is also
    /// what the W3C reftest vocabulary calls this artefact, which is where the corpus's structure
    /// comes from.
    /// </remarks>
    public const string ReferencePrefix = "reference_";

    /// <summary>The base name the corpus tests snapshot under.</summary>
    public const string ResultName = "result";

    /// <summary>This file's own directory, which is the test project root.</summary>
    /// <remarks>
    /// Resolved from the compiler rather than from ProjectDefaults' generated
    /// <c>ProjectFiles</c>: this file is linked into RefGen too, where that class would point at
    /// RefGen's directory instead of at the corpus.
    /// </remarks>
    public static string ProjectDirectory { get; } = Directory(ThisFile());

    /// <summary>The corpus root, holding one directory per category.</summary>
    public static string InputsDirectory { get; } = Path.Combine(ProjectDirectory, "Inputs");

    /// <summary>The bundled faces every scenario is rendered with.</summary>
    public static string FontsDirectory { get; } = Path.Combine(ProjectDirectory, "Fonts");

    /// <summary>Every scenario directory, in a stable order.</summary>
    /// <remarks>
    /// Empty rather than throwing when the corpus is absent, so the smoke tests and the markdown
    /// regeneration hook both work in a tree where no scenario has been committed yet.
    /// </remarks>
    public static IEnumerable<string> Directories() =>
        System.IO.Directory.Exists(InputsDirectory)
            ? System.IO.Directory
                .EnumerateFiles(InputsDirectory, HtmlFile, SearchOption.AllDirectories)
                .Select(_ => Directory(_))
                .OrderBy(_ => _, StringComparer.OrdinalIgnoreCase)
            : [];

    /// <summary>Scenario directories under one category.</summary>
    public static IEnumerable<string> Directories(string category) =>
        Directories()
            .Where(_ => Name(_).StartsWith($"{category}/", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A scenario's name: its path below the corpus root, with forward slashes.
    /// </summary>
    public static string Name(string directory) =>
        Path.GetRelativePath(InputsDirectory, Path.GetFullPath(directory)).Replace('\\', '/');

    /// <summary>
    /// A scenario's full HTML: its markup, with the shared reset and its own stylesheet inlined.
    /// </summary>
    /// <remarks>
    /// Inlined rather than linked so the browser and the converter receive byte-identical input.
    /// A <c>&lt;link&gt;</c> would make the browser fetch the stylesheet and leave the converter
    /// to resolve it separately, which is one more thing that could differ between the two sides
    /// of a comparison that exists to attribute differences to layout.
    /// </remarks>
    public static string Html(string directory)
    {
        var markup = File.ReadAllText(Path.Combine(directory, HtmlFile));
        var reset = File.ReadAllText(Path.Combine(InputsDirectory, ResetFile));

        var cssPath = Path.Combine(directory, CssFile);
        var css = File.Exists(cssPath) ? File.ReadAllText(cssPath) : "";

        return $"""
                <!doctype html>
                <html>
                <head>
                <meta charset="utf-8">
                <style>
                {reset}
                </style>
                <style>
                {css}
                </style>
                </head>
                <body>
                {markup}
                </body>
                </html>
                """;
    }

    /// <summary>The reference page renders in a scenario directory, in page order.</summary>
    public static string[] ReferencePages(string directory) =>
        System.IO.Directory.GetFiles(directory, $"{ReferencePrefix}*.png").Order().ToArray();

    /// <summary>The file a reference page is stored as.</summary>
    public static string ReferencePage(string directory, int pageNumber) =>
        Path.Combine(directory, $"{ReferencePrefix}{pageNumber:0000}.png");

    /// <summary>The file the browser's element geometry is stored in.</summary>
    public static string BoxesPath(string directory) =>
        Path.Combine(directory, BoxesFile);

    static string Directory(string path) =>
        Path.GetDirectoryName(path)!;

    static string ThisFile([CallerFilePath] string path = "") =>
        path;
}
