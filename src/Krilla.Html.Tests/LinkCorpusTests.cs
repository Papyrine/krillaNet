/// <inheritdoc cref="BlockCorpusTests" />
public class LinkCorpusTests
{
    public static IEnumerable<string> Scenarios() =>
        CorpusLayout.Directories("link");

    [Test]
    [MethodDataSource(nameof(Scenarios))]
    public Task Scenario(string directory) =>
        CorpusRunner.Run(directory);
}