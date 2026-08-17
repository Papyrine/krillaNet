/// <summary>
/// Regenerates the browser reference for every corpus scenario: the page renders
/// (<c>reference_0001.png</c>) and the element geometry (<c>reference.boxes.json</c>).
/// </summary>
/// <remarks>
/// <para>
/// Explicit, and a separate project from the tests, for the same reason Morph keeps
/// <c>RenderHelper</c> apart from its suite: a reference is an input to the comparison, not an
/// output of it. Regenerating one during a test run would mean the suite could never fail — it
/// would move the target to wherever the render landed. So references are produced deliberately,
/// reviewed as a diff, and committed.
/// </para>
/// <para>
/// Run it after adding a scenario, or after deliberately changing the shared reset stylesheet.
/// Never to make a failing comparison pass.
/// </para>
/// </remarks>
[Explicit]
public class ReferenceGenerator
{
    [Test]
    public async Task GenerateReferences()
    {
        var scenarios = CorpusLayout.Directories().ToList();
        if (scenarios.Count == 0)
        {
            Console.WriteLine($"No scenarios found under {CorpusLayout.InputsDirectory}.");
            return;
        }

        // Chromium is not installed from here on purpose. A generator that silently pulls a
        // 150MB browser mid-run is a surprise on a developer machine and a hang on a CI agent, so
        // the download stays a deliberate one-time step the readme documents:
        //
        //   dotnet tool install --global Microsoft.Playwright.CLI
        //   playwright install chromium
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            Args =
            [
                // Lets the file:// page load the file:// @font-face URLs below.
                "--allow-file-access-from-files",
                // Strip platform-specific text rasterization so two machines produce the same
                // reference: no hinting, no LCD subpixel anti-aliasing, a fixed sRGB profile.
                // Without these the reference drifts between a local box and CI, and every metric
                // measured against it drifts with it.
                "--font-render-hinting=none",
                "--disable-lcd-text",
                "--force-color-profile=srgb"
            ]
        });

        foreach (var directory in scenarios)
        {
            await Generate(browser, directory);
            Console.WriteLine($"  {CorpusLayout.Name(directory)}");
        }

        Console.WriteLine($"Regenerated {scenarios.Count} scenarios.");
    }

    static async Task Generate(IBrowser browser, string directory)
    {
        await using var context = await browser.NewContextAsync(new()
        {
            ViewportSize = new()
            {
                Width = CorpusLayout.PageWidth,
                Height = CorpusLayout.PageHeight
            },
            // One device pixel per CSS pixel, so the geometry the script reports and the pixels
            // the render produces are in the same units as our layout.
            DeviceScaleFactor = 1
        });

        var page = await context.NewPageAsync();

        // Navigated to as a real file:// page rather than set with SetContent, which runs on an
        // opaque origin that blocks local font files — and without the bundled fonts the reference
        // would be rendered in whatever the host happens to have installed.
        //
        // Written into the scenario's own directory rather than a temp one, so a relative img src
        // resolves against the same base the converter is handed. From a temp directory it would
        // resolve to nothing and the browser reference would quietly contain no images.
        var temporary = CorpusLayout.BrowserPagePath(directory);
        await File.WriteAllTextAsync(temporary, Document(directory));

        try
        {
            await page.GotoAsync(new Uri(temporary).AbsoluteUri, new()
            {
                WaitUntil = WaitUntilState.Load
            });

            // Block until every @font-face has loaded, so nothing is measured or captured while a
            // fallback face is still showing.
            await page.EvaluateAsync("async () => { await document.fonts.ready; }");

            await WriteBoxes(page, directory);
            await WritePages(page, directory);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    /// <summary>
    /// Harvests every element's border box, keyed by the same selector path the layout engine
    /// builds.
    /// </summary>
    /// <remarks>
    /// Taken from the screen layout at the corpus viewport, not from the print layout, because
    /// that is what our own continuous layout corresponds to — the box tree is laid out as one
    /// column and sliced into pages afterwards.
    ///
    /// Coordinates are made document-relative by adding the scroll offset, so a scenario taller
    /// than the viewport reports the same numbers as one that fits.
    /// </remarks>
    static async Task WriteBoxes(IPage page, string directory)
    {
        var json = await page.EvaluateAsync<string>(
            """
            () => {
              const path = element => {
                const segments = [];
                for (let node = element; node; node = node.parentElement) {
                  if (!node.parentElement) {
                    segments.unshift(node.localName);
                    continue;
                  }
                  let index = 1;
                  for (const sibling of node.parentElement.children) {
                    if (sibling === node) break;
                    index++;
                  }
                  segments.unshift(`${node.localName}:nth-child(${index})`);
                }
                return segments.join(' > ');
              };

              const round = value => Math.round(value * 100) / 100;

              const boxes = [];
              for (const element of document.documentElement.querySelectorAll('*')) {
                // Elements that generate no box report an all-zero rect. Keeping them would make
                // a display:none scenario compare zeros against boxes we correctly never built.
                const style = getComputedStyle(element);
                if (style.display === 'none') continue;
                if (element.localName === 'style' || element.localName === 'script') continue;

                const rect = element.getBoundingClientRect();
                boxes.push({
                  selector: path(element),
                  x: round(rect.x + window.scrollX),
                  y: round(rect.y + window.scrollY),
                  width: round(rect.width),
                  height: round(rect.height)
                });
              }

              const root = document.documentElement.getBoundingClientRect();
              boxes.unshift({
                selector: path(document.documentElement),
                x: round(root.x + window.scrollX),
                y: round(root.y + window.scrollY),
                width: round(root.width),
                height: round(root.height)
              });

              return JSON.stringify(boxes, null, 2);
            }
            """);

        await File.WriteAllTextAsync(CorpusLayout.BoxesPath(directory), json + "\n");
    }

    /// <summary>
    /// Renders the scenario's pages by printing to PDF and rasterising that.
    /// </summary>
    /// <remarks>
    /// Printing rather than screenshotting, which is the significant choice here. A full-page
    /// screenshot sliced into page-height strips would cut a line of text in half at every
    /// boundary, whereas both Chromium's printer and our own paginator break between lines — so a
    /// sliced screenshot would report a difference at every page break that is an artefact of how
    /// the reference was made rather than of anything either engine did.
    ///
    /// Rasterising through PDFium at the corpus DPI also means both sides of the comparison come
    /// out of the same rasteriser, which removes one more source of difference that is not layout.
    /// </remarks>
    static async Task WritePages(IPage page, string directory)
    {
        var pdf = await page.PdfAsync(new()
        {
            // Inches, because that is what Playwright's page size takes. The corpus page is
            // defined in CSS pixels at 96 to the inch.
            Width = $"{CorpusLayout.PageWidth / 96f}in",
            Height = $"{CorpusLayout.PageHeight / 96f}in",
            // Without this every background-color in the corpus prints as nothing.
            PrintBackground = true,
            Margin = new()
            {
                Top = "0",
                Right = "0",
                Bottom = "0",
                Left = "0"
            },
            PreferCSSPageSize = false,
            Scale = 1
        });

        foreach (var stale in CorpusLayout.ReferencePages(directory))
        {
            File.Delete(stale);
        }

        using var document = PdfiumDocument.Load(pdf);

        for (var index = 0; index < document.PageCount; index++)
        {
            var png = document.RenderPage(index, new RenderOptions
            {
                Dpi = CorpusLayout.Dpi
            });

            await File.WriteAllBytesAsync(CorpusLayout.ReferencePage(directory, index + 1), png);
        }
    }

    /// <summary>
    /// The scenario's HTML with the bundled fonts bound to the families the corpus names.
    /// </summary>
    /// <remarks>
    /// The <c>@font-face</c> rules are what pin the browser to the same files the converter loads.
    /// Without them Chromium resolves <c>sans-serif</c> against the host's installed fonts, text
    /// reflows between machines, and every number the corpus records becomes noise.
    /// </remarks>
    static string Document(string directory)
    {
        var html = CorpusLayout.Html(directory);
        return html.Replace("<head>", $"<head><style>{FontFaces()}</style>", StringComparison.Ordinal);
    }

    static string FontFaces()
    {
        var builder = new StringBuilder();

        foreach (var path in Directory.EnumerateFiles(CorpusLayout.FontsDirectory, "*.ttf").Order())
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var family = name.Split('-')[0] switch
            {
                "LiberationSans" => "Liberation Sans",
                "LiberationSerif" => "Liberation Serif",
                "LiberationMono" => "Liberation Mono",
                _ => name
            };

            var bold = name.Contains("Bold", StringComparison.Ordinal);
            var italic = name.Contains("Italic", StringComparison.Ordinal);

            builder.Append("@font-face{font-family:\"").Append(family).Append("\";");
            builder.Append("font-weight:").Append(bold ? "700" : "400").Append(';');
            builder.Append("font-style:").Append(italic ? "italic" : "normal").Append(';');
            builder.Append("src:url(\"").Append(new Uri(path).AbsoluteUri).Append("\");}");
        }

        return builder.ToString();
    }
}
