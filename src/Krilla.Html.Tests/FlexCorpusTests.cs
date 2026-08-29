/// <inheritdoc cref="BlockCorpusTests"/>
public class FlexCorpusTests
{
    public static IEnumerable<string> Scenarios() =>
        CorpusLayout.Directories("flex");

    [Test]
    [MethodDataSource(nameof(Scenarios))]
    public Task Scenario(string directory) =>
        CorpusRunner.Run(directory);
}
