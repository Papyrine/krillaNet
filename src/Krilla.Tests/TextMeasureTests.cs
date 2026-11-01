using TUnit.Assertions.Enums;

/// <summary>
/// <see cref="Font.MeasureText"/> and <see cref="Font.WrapText"/>, against Liberation Sans.
/// </summary>
public class TextMeasureTests
{
    static readonly Font sans = TestFonts.LoadSans();

    const string pangram = "The quick brown fox jumps over the lazy dog";

    [Test]
    public async Task MeasuresInTheUnitsOfTheSize()
    {
        // Liberation Sans is metric-compatible with Arial, whose H advances 1479 of 2048 units.
        // At a size of 2048 the width therefore comes back in design units.
        await Assert.That(sans.MeasureText("H", 2048)).IsEqualTo(1479f);
        await Assert.That(sans.MeasureText("HH", 2048)).IsEqualTo(2958f);
    }

    [Test]
    public async Task WidthScalesWithSize()
    {
        var small = sans.MeasureText(pangram, 12);
        var large = sans.MeasureText(pangram, 24);

        await Assert.That(large).IsEqualTo(small * 2).Within(0.001f);
    }

    [Test]
    public async Task EmptyTextHasNoWidth() =>
        await Assert.That(sans.MeasureText("", 12)).IsEqualTo(0f);

    [Test]
    public async Task ShortTextIsOneLine()
    {
        var lines = sans.WrapText(pangram, 12, 1000);

        await Assert.That(lines).IsEquivalentTo([pangram], CollectionOrdering.Matching);
    }

    [Test]
    public async Task BreaksAtSpaces()
    {
        var lines = sans.WrapText(pangram, 12, 100);

        await Assert.That(lines).IsEquivalentTo(
            [
                "The quick brown",
                "fox jumps over the",
                "lazy dog"
            ], CollectionOrdering.Matching);

        foreach (var line in lines)
        {
            await Assert.That(sans.MeasureText(line, 12)).IsLessThanOrEqualTo(100f);
        }
    }

    [Test]
    public async Task EachLineIsAsFullAsItCanBe()
    {
        var lines = sans.WrapText(pangram, 12, 100);

        // Greedy: the first word of each next line would not have fitted on the one before.
        for (var i = 0; i + 1 < lines.Count; i++)
        {
            var next = lines[i + 1].Split(' ')[0];
            await Assert.That(sans.MeasureText($"{lines[i]} {next}", 12)).IsGreaterThan(100f);
        }
    }

    [Test]
    public async Task BreaksInsideAWordOnlyWhenTheWordAloneIsTooWide()
    {
        const string word = "Pneumonoultramicroscopicsilicovolcanoconiosis";
        var lines = sans.WrapText($"An {word} is long", 12, 80);

        // "An" keeps its own line rather than taking the start of the long word with it, the
        // word is cut into pieces that fit, and its last piece is joined by what follows.
        await Assert.That(lines).IsEquivalentTo(
            [
                "An",
                "Pneumonoultr",
                "amicroscopicsi",
                "licovolcanocon",
                "iosis is long"
            ], CollectionOrdering.Matching);

        foreach (var line in lines)
        {
            await Assert.That(sans.MeasureText(line, 12)).IsLessThanOrEqualTo(80f);
        }

        // Each cut is as late as it can be: one more character would not have fitted.
        for (var i = 1; i + 1 < lines.Count; i++)
        {
            var longer = lines[i] + lines[i + 1][0];
            await Assert.That(sans.MeasureText(longer, 12)).IsGreaterThan(80f);
        }
    }

    [Test]
    public async Task KeepsLineBreaks()
    {
        var lines = sans.WrapText("one\n\r\ntwo\rthree", 12, 1000);

        await Assert.That(lines).IsEquivalentTo(["one", "", "two", "three"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task CollapsesSpaces()
    {
        var lines = sans.WrapText("  a    b  ", 12, 1000);

        await Assert.That(lines).IsEquivalentTo(["a b"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task EmptyTextIsOneEmptyLine()
    {
        var lines = sans.WrapText("", 12, 100);

        await Assert.That(lines).IsEquivalentTo([""], CollectionOrdering.Matching);
    }

    [Test]
    public async Task ACharacterWiderThanTheLineStillAdvances()
    {
        var lines = sans.WrapText("WWW", 12, 1);

        await Assert.That(lines).IsEquivalentTo(["W", "W", "W"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task NeverSplitsASurrogatePair()
    {
        // U+1F600 is two UTF-16 code units. A cut between them would draw two halves of nothing.
        var lines = sans.WrapText("😀😀😀", 12, 1);

        await Assert.That(lines).IsEquivalentTo(["😀", "😀", "😀"], CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(0f)]
    [Arguments(-1f)]
    [Arguments(float.NaN)]
    public async Task WidthMustBePositive(float width) =>
        await Assert.That(() => sans.WrapText("a", 12, width)).Throws<ArgumentOutOfRangeException>();
}
