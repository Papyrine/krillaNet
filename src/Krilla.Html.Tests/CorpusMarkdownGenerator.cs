/// <summary>
/// Generates the side-by-side markdown the corpus is read through: a per-scenario
/// <c>compare.md</c>, and an aggregate <c>compare-all.md</c> at the corpus root.
/// </summary>
/// <remarks>
/// The numbers say how far off a scenario is; these say what it looks like. Both matter, and for
/// different failures — a layout can score well while putting a heading through a paragraph, and
/// can score badly while being visibly fine because one font substituted. Rendering cleanly on
/// GitHub is the requirement, so the tables are HTML-in-markdown rather than anything richer.
/// </remarks>
static class CorpusMarkdownGenerator
{
    public static void Regenerate(string directory)
    {
        var builder = new StringBuilder();
        builder.Append($"# {CorpusLayout.Name(directory)}\n\n");
        AppendNotes(builder, directory);
        AppendScenario(builder, directory, prefix: "");

        // Scenarios run concurrently and a contended write on Windows can fail with "user-mapped
        // section open". Swallow it: RegenerateAll runs at process exit as the reconciliation
        // pass, so a lost write here costs nothing.
        try
        {
            File.WriteAllText(Path.Combine(directory, "compare.md"), builder.ToString());
        }
        catch (IOException)
        {
        }
    }

    public static void RegenerateAll()
    {
        var scenarios = CorpusLayout.Directories().ToArray();
        if (scenarios.Length == 0)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.Append($"# All scenarios ({scenarios.Length})\n\n");
        // Kept free of the words MarkdownSnippets rejects — this text lands in a committed .md,
        // so the content validator sees it on the next build.
        builder.Append(
            "The browser reference (left) beside the page Krilla.Html produced (right). " +
            "`AE` is the fraction of pixels that differ and `SSIM` is structural similarity; " +
            "neither is asserted. The worst offset is the largest positional disagreement in CSS " +
            "pixels between the rendered element geometry and the browser's, and is the number to " +
            "watch — it reaches zero exactly when the layout is right.\n\n");

        AppendContents(builder, scenarios);

        foreach (var directory in scenarios)
        {
            var name = CorpusLayout.Name(directory);
            builder.Append($"## {name}\n\n");
            AppendNotes(builder, directory);
            AppendScenario(builder, directory, prefix: $"{name}/");
            builder.Append('\n');
        }

        try
        {
            File.WriteAllText(
                Path.Combine(CorpusLayout.InputsDirectory, "compare-all.md"),
                builder.ToString());
        }
        catch (IOException)
        {
        }
    }

    static void AppendScenario(StringBuilder builder, string directory, string prefix)
    {
        var result = ReadResult(directory);
        AppendBoxes(builder, result);

        var referencePages = CorpusLayout.ReferencePages(directory)
            .Select(Path.GetFileName)
            .ToArray();

        var renderedPages = Directory
            .GetFiles(directory, $"{CorpusLayout.ResultName}#page_*.verified.png")
            .Order()
            .Select(Path.GetFileName)
            .ToArray();

        if (referencePages.Length == 0 && renderedPages.Length == 0)
        {
            builder.Append("_No pages rendered yet._\n\n");
            return;
        }

        builder.Append("| Reference (Chrome) | Krilla.Html |\n| --- | --- |\n");

        // Rows run to the longer side so a pagination difference is visible as a blank cell rather
        // than as pages quietly missing from the bottom of the table.
        var pageCount = Math.Max(referencePages.Length, renderedPages.Length);

        for (var index = 0; index < pageCount; index++)
        {
            var reference = index < referencePages.Length ? referencePages[index] : null;
            var rendered = index < renderedPages.Length ? renderedPages[index] : null;
            var metrics = result?.PageDiffs?.FirstOrDefault(_ => _.Page == index + 1);

            builder.Append("| ");
            builder.Append(Label($"Page {index + 1}", reference, null));
            builder.Append(" | ");
            builder.Append(Label($"Page {index + 1}", rendered, metrics));
            builder.Append(" |\n| ");
            builder.Append(Image(reference, prefix));
            builder.Append(" | ");
            builder.Append(Image(rendered, prefix));
            builder.Append(" |\n");
        }

        builder.Append('\n');
    }

    static void AppendBoxes(StringBuilder builder, CorpusResult? result)
    {
        if (result?.Boxes is not {} boxes)
        {
            return;
        }

        builder.Append(
            $"**Boxes**: {boxes.Matched} matched, worst offset {boxes.WorstOffset:F2}px, " +
            $"worst size {boxes.WorstSize:F2}px.\n\n");

        if (boxes.MissingFromRender.Count > 0)
        {
            builder.Append($"Not rendered: `{string.Join("`, `", boxes.MissingFromRender)}`\n\n");
        }

        if (boxes.NotInReference.Count > 0)
        {
            builder.Append($"Absent from the reference: `{string.Join("`, `", boxes.NotInReference)}`\n\n");
        }

        if (boxes.Diffs.Count == 0)
        {
            return;
        }

        // The worst handful, because the point of the table is to say where to look next. The full
        // list is in result.verified.json, and the count below says how much was left out so the
        // truncation cannot read as "that was all of them".
        const int shown = 5;

        builder.Append("<details>\n<summary>Box differences</summary>\n\n");
        builder.Append("| Element | dx | dy | dw | dh |\n| --- | --- | --- | --- | --- |\n");

        foreach (var diff in boxes.Diffs
                     .OrderByDescending(_ => Math.Max(Math.Abs(_.Dx), Math.Abs(_.Dy)))
                     .Take(shown))
        {
            builder.Append(
                $"| `{diff.Selector}` | {diff.Dx:F2} | {diff.Dy:F2} | {diff.Dw:F2} | {diff.Dh:F2} |\n");
        }

        if (boxes.Diffs.Count > shown)
        {
            builder.Append($"\n_{boxes.Diffs.Count - shown} further differences not shown._\n");
        }

        builder.Append("\n</details>\n\n");
    }

    static void AppendNotes(StringBuilder builder, string directory)
    {
        var path = Path.Combine(directory, "notes.md");
        if (!File.Exists(path))
        {
            return;
        }

        var content = File.ReadAllText(path).AsSpan().Trim();
        if (content.Length > 0)
        {
            builder.Append(content).Append("\n\n");
        }
    }

    static void AppendContents(StringBuilder builder, IEnumerable<string> directories)
    {
        builder.Append("<details>\n<summary>Contents</summary>\n\n");

        foreach (var directory in directories)
        {
            var name = CorpusLayout.Name(directory);
            builder.Append($"- [{name}](#{Anchor(name)})\n");
        }

        builder.Append("\n</details>\n\n");
    }

    static CorpusResult? ReadResult(string directory)
    {
        var path = Path.Combine(directory, $"{CorpusLayout.ResultName}.verified.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), CorpusResultJson.Default.CorpusResult);
        }
        catch (JsonException)
        {
            // A scenario whose snapshot predates a shape change will not deserialize. The markdown
            // is a convenience, so degrade to "no metrics" rather than taking the test run down
            // with it.
            return null;
        }
    }

    static string Label(string page, string? file, PageDiff? metrics)
    {
        if (file is null)
        {
            return $"**{page}** _(no page)_";
        }

        if (metrics is null)
        {
            return $"**{page}**";
        }

        var ssim = metrics.Ssim is {} value ? $" · SSIM {value:F4}" : "";
        return $"**{page}. AE {metrics.AbsoluteError:F4}{ssim}**";
    }

    static string Image(string? file, string prefix) =>
        file is null ? "" : $"""<img src="{Encode(prefix + file)}" width="480">""";

    static string Encode(string path) =>
        path.Replace("#", "%23");

    /// <summary>Mirrors GitHub's heading-anchor algorithm.</summary>
    static string Anchor(string heading)
    {
        var builder = new StringBuilder(heading.Length);

        foreach (var character in heading)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (character is '-' or '_')
            {
                builder.Append(character);
            }
            else if (char.IsWhiteSpace(character) || character is '/')
            {
                builder.Append('-');
            }
        }

        return builder.ToString();
    }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(CorpusResult))]
partial class CorpusResultJson : JsonSerializerContext;
