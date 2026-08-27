/// <summary>
/// Coverage-driven font fallback: which face draws a character the resolved one lacks.
/// </summary>
/// <remarks>
/// <para>
/// Not measurable by the corpus, and not for the usual reason. The reference generator binds the
/// bundled families through <c>@font-face</c> so both engines load the same files; a character
/// none of them covers is then resolved by Chromium against whatever the HOST has installed, which
/// is exactly the thing the corpus exists to keep out of its numbers. So the comparison would
/// measure the machine rather than the fallback.
/// </para>
/// <para>
/// The bundled set is what makes these tests possible at all: Liberation Sans carries the planet
/// symbols at U+263F..U+2647 and Liberation Mono and Serif do not, which is a real coverage gap
/// between three faces that are otherwise metric-compatible.
/// </para>
/// </remarks>
public class FontFallbackTests
{
    /// <summary>A character Liberation Sans has and neither Mono nor Serif does.</summary>
    const string saturn = "♄";

    /// <summary>One Liberation Sans and Serif both have and Mono does not.</summary>
    const string dotless = "ȷ";

    [Test]
    public async Task ACoveredDocumentIsOneRunInOneFace()
    {
        var runs = await Runs("<p>plain latin text</p>", "Liberation Mono");

        await Assert.That(runs).Count().IsEqualTo(1);
        await Assert.That(runs[0].Face.Family).IsEqualTo("Liberation Mono");
    }

    /// <summary>
    /// A character the resolved face lacks is drawn by one that has it.
    /// </summary>
    /// <remarks>
    /// The defect this exists for. Family resolution answers a <c>font-family</c> list and nothing
    /// else, so a character outside the face it chose was drawn as <c>.notdef</c> — a row of boxes,
    /// silently, with the resolution having done exactly what it was asked.
    /// </remarks>
    [Test]
    public async Task AnUncoveredCharacterFallsBackToAFaceThatHasIt()
    {
        var runs = await Runs($"<p>a{saturn}b</p>", "Liberation Mono");

        await Assert.That(runs.Select(_ => _.Face.Family).ToArray())
            .IsEquivalentTo(["Liberation Mono", "Liberation Sans", "Liberation Mono"]);

        await Assert.That(runs.Select(_ => _.Text).ToArray())
            .IsEquivalentTo(["a", saturn, "b"]);
    }

    /// <summary>
    /// The rest of the element's own stack is searched before anything else registered.
    /// </summary>
    /// <remarks>
    /// What a font stack is FOR: an author naming two families has said which face should answer
    /// for a character the first one lacks. The dotless j is what separates the two searches —
    /// Liberation Sans and Serif both carry it and Mono does not, and registration order reaches
    /// Sans first, so naming Serif in the stack has to win.
    /// </remarks>
    [Test]
    public async Task TheRestOfTheDeclaredStackIsPreferred()
    {
        var declared = await Runs(
            $"<p>a{dotless}b</p>",
            "\"Liberation Mono\", \"Liberation Serif\"");

        await Assert.That(declared[1].Face.Family).IsEqualTo("Liberation Serif");

        // With nothing else named, the same character takes the first registered face that has it.
        var registered = await Runs($"<p>a{dotless}b</p>", "\"Liberation Mono\"");

        await Assert.That(registered[1].Face.Family).IsEqualTo("Liberation Sans");
    }

    /// <summary>
    /// A character nothing covers keeps the face the document asked for.
    /// </summary>
    /// <remarks>
    /// The <c>.notdef</c> belongs in the declared face rather than in whichever face happened to be
    /// looked at last — which is also what keeps the run from splitting for no reason.
    /// </remarks>
    [Test]
    public async Task ACharacterNothingCoversStaysInTheDeclaredFace()
    {
        var runs = await Runs("<p>a中b</p>", "Liberation Mono");

        await Assert.That(runs).Count().IsEqualTo(1);
        await Assert.That(runs[0].Face.Family).IsEqualTo("Liberation Mono");
    }

    /// <summary>
    /// A change of face inside a word is not a break opportunity.
    /// </summary>
    /// <remarks>
    /// The rule the split had to preserve. A word crossing a coverage boundary becomes two tokens,
    /// and two adjacent tokens are exactly the arrangement <c>inline/word_joins</c> proved a line
    /// must not break between — so the fallback would have reintroduced that defect by another
    /// route. Measured by squeezing the word into a box narrower than it is: it overflows on one
    /// line rather than breaking at the symbol.
    /// </remarks>
    [Test]
    public async Task AWordCrossingACoverageBoundaryDoesNotBreak()
    {
        var runs = await Runs(
            $"<p style=\"width: 30px\">aaaaa{saturn}aaaaa</p>",
            "Liberation Mono");

        await Assert.That(runs.Select(_ => _.Y).Distinct()).Count().IsEqualTo(1);
    }

    /// <summary>
    /// The pieces of a split word measure exactly what the whole would have.
    /// </summary>
    /// <remarks>
    /// Each run is shaped over its own substring rather than sliced out of one shaping, which is
    /// forced: a shaper works in one face. The widths still have to sum to the same advance the
    /// characters ask for, or a line breaks in the wrong place for a reason nothing points at.
    /// </remarks>
    [Test]
    public async Task TheSplitRunsSumToTheWholeAdvance()
    {
        var split = await Runs($"<p>aaaaa{saturn}aaaaa</p>", "Liberation Mono");
        var plain = await Runs("<p>aaaaaaaaaa</p>", "Liberation Mono");

        var mono = CorpusRunner.Options().Fonts!.Resolve(["Liberation Sans"], 400, italic: false);
        var symbol = mono.Advance(saturn[0], 16);

        await Assert.That(split.Sum(_ => _.Width)).IsEqualTo(plain[0].Width + symbol).Within(0.01f);
    }

    static async Task<IReadOnlyList<TextRun>> Runs(string body, string family)
    {
        var html =
            "<!doctype html><html><head><style>" +
            "*{margin:0;padding:0;border:none}" +
            $"html{{font-family:{family};font-size:16px}}" +
            "</style></head><body>" + body + "</body></html>";

        var options = CorpusRunner.Options();

        using var document = await HtmlConverter.ParseAsync(html, options);
        using var layout = HtmlConverter.LayoutDocument(document, HtmlConverter.Paged(document, options, out _));

        return layout.Root
            .Descendants()
            .SelectMany(_ => _.Lines)
            .SelectMany(_ => _.Runs)
            .ToArray();
    }
}
