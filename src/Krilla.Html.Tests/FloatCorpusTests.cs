/// <summary>
/// Floats: boxes out of flow, and the line boxes that make room for them.
/// </summary>
/// <remarks>
/// Separate from <c>block</c> because a float breaks the assumption every block scenario rests on
/// — that a box is positioned by its predecessors alone. A float placed in one block reaches
/// forward to shorten the lines of a later sibling, so these scenarios are measuring an
/// interaction rather than a box.
/// </remarks>
public class FloatCorpusTests
{
    public static IEnumerable<string> Scenarios() =>
        CorpusLayout.Directories("float");

    [Test]
    [MethodDataSource(nameof(Scenarios))]
    public Task Scenario(string directory) =>
        CorpusRunner.Run(directory);
}