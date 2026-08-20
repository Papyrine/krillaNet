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

/// <summary>
/// Text shaping: kerning and ligatures, which the corpus could not measure before shaping existed.
/// </summary>
public class TextCorpusTests
{
    public static IEnumerable<string> Scenarios() =>
        CorpusLayout.Directories("text");

    [Test]
    [MethodDataSource(nameof(Scenarios))]
    public Task Scenario(string directory) =>
        CorpusRunner.Run(directory);
}

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
