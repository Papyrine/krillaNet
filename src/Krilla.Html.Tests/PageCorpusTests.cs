/// <inheritdoc cref="BlockCorpusTests" />
public class PageCorpusTests
{
    public static IEnumerable<string> Scenarios() =>
        CorpusLayout.Directories("page");

    [Test]
    [MethodDataSource(nameof(Scenarios))]
    public Task Scenario(string directory) =>
        CorpusRunner.Run(directory);
}