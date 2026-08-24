/// <summary>
/// Text shaping: kerning and ligatures, which the corpus could not measure before shaping existed.
/// </summary>
public class TextCorpusTests
{
    public static IEnumerable<string> Scenarios() =>
        CorpusLayout.Directories("text");

    [Test]
    [MethodDataSource(nameof(Scenarios))]
    public Task Scenario(string directory) =>
        CorpusRunner.Run(directory);
}