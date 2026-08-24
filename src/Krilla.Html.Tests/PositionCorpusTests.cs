/// <summary>
/// Positioned boxes: offsets applied after flow, and boxes taken out of it.
/// </summary>
/// <remarks>
/// Its own category because the question these ask is which box another box is measured against,
/// rather than where flow put it. A positioned box can be anchored to an ancestor several levels
/// up, or to the page, so a difference here localises to a containing block rather than to a
/// stacking position.
/// </remarks>
public class PositionCorpusTests
{
    public static IEnumerable<string> Scenarios() =>
        CorpusLayout.Directories("position");

    [Test]
    [MethodDataSource(nameof(Scenarios))]
    public Task Scenario(string directory) =>
        CorpusRunner.Run(directory);
}