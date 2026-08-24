/// <summary>
/// Tables: the one formatting context whose parts cannot be measured independently.
/// </summary>
/// <remarks>
/// Worth its own category rather than living under <c>block</c>, because a table's numbers come
/// from a different algorithm rather than from a variation on block layout — a column's width is a
/// property of every cell in it, so nothing here can be checked by looking at one box.
/// </remarks>
public class TableCorpusTests
{
    public static IEnumerable<string> Scenarios() =>
        CorpusLayout.Directories("table");

    [Test]
    [MethodDataSource(nameof(Scenarios))]
    public Task Scenario(string directory) =>
        CorpusRunner.Run(directory);
}