/// <summary>
/// The corpus, one test class per category.
/// </summary>
/// <remarks>
/// Split by category rather than driven from one class over the whole corpus because TUnit cannot
/// filter on parameter values — running just the block scenarios needs its own class to point
/// <c>--treenode-filter</c> at. Each class is its data source and a one-line call to
/// <see cref="CorpusRunner"/>.
/// </remarks>
public class BlockCorpusTests
{
    public static IEnumerable<string> Scenarios() =>
        CorpusLayout.Directories("block");

    [Test]
    [MethodDataSource(nameof(Scenarios))]
    public Task Scenario(string directory) =>
        CorpusRunner.Run(directory);
}

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
