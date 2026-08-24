/// <summary>
/// The default stylesheet, measured rather than flattened away.
/// </summary>
/// <remarks>
/// These scenarios carry a <c>no-flatten</c> marker so they keep the defaults the rest of the
/// corpus deliberately removes. They are the only ones asking whether an unstyled document renders
/// correctly, which is the question every real document asks.
/// </remarks>
public class UserAgentCorpusTests
{
    public static IEnumerable<string> Scenarios() =>
        CorpusLayout.Directories("ua");

    [Test]
    [MethodDataSource(nameof(Scenarios))]
    public Task Scenario(string directory) =>
        CorpusRunner.Run(directory);
}