/// <inheritdoc cref="BlockCorpusTests" />
public class InlineCorpusTests
{
    public static IEnumerable<string> Scenarios() =>
        CorpusLayout.Directories("inline");

    [Test]
    [MethodDataSource(nameof(Scenarios))]
    public Task Scenario(string directory) =>
        CorpusRunner.Run(directory);
}