/// <summary>
/// The whole corpus's geometry against the browser's, as one snapshot.
/// </summary>
/// <remarks>
/// <para>
/// The primary fidelity gate, and deliberately separate from <see cref="CorpusRunner"/>'s
/// per-scenario snapshots. Two reasons it earns its own test:
/// </para>
/// <para>
/// It reads at a glance. Twenty-five files each holding one scenario's numbers answer "did this
/// scenario change"; one table answers "is the engine getting closer", which is the question being
/// worked on.
/// </para>
/// <para>
/// And it needs no native library. Layout runs entirely in managed code — krilla is only reached
/// when a PDF is painted — so this measures the engine on a machine with no Rust toolchain, where
/// every PDF-producing test cannot run at all.
/// </para>
/// </remarks>
public class BoxFidelityTests
{
    [Test]
    public Task Corpus()
    {
        var options = CorpusRunner.Options();
        var report = new StringBuilder();

        report.Append(
            "Layout geometry against the browser reference, in CSS pixels. Zero means the box tree\n" +
            "agrees with Chrome exactly. `unmatched` counts elements the browser laid out that this\n" +
            "engine generates no box for.\n\n");

        foreach (var directory in CorpusLayout.Directories())
        {
            var name = CorpusLayout.Name(directory);
            var path = CorpusLayout.BoxesPath(directory);

            if (!File.Exists(path))
            {
                report.Append($"{name,-32} no reference\n");
                continue;
            }

            var reference = JsonSerializer.Deserialize(
                                File.ReadAllText(path),
                                CorpusJson.Default.ListBoxGeometry) ??
                            [];

            var result = BoxComparison.Compare(
                reference,
                BoxDump.Measure(CorpusLayout.Html(directory), options));

            report.Append(
                $"{name,-32} matched {result.Matched,2}  " +
                $"offset {result.WorstOffset,7:F2}  " +
                $"size {result.WorstSize,7:F2}  " +
                $"differing {result.Diffs.Count,2}  " +
                $"unmatched {result.MissingFromRender.Count + result.NotInReference.Count}\n");
        }

        return Verify(report.ToString());
    }
}
