/// <inheritdoc cref="BlockCorpusTests" />
public class ImageCorpusTests
{
    public static IEnumerable<string> Scenarios() =>
        CorpusLayout.Directories("image");

    [Test]
    [MethodDataSource(nameof(Scenarios))]
    public Task Scenario(string directory) =>
        CorpusRunner.Run(directory);
}