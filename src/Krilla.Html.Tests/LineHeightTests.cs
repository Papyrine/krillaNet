/// <summary>
/// How <c>line-height</c> resolves and inherits.
/// </summary>
/// <remarks>
/// Its own class because the property has two forms that behave differently on inheritance, and
/// the difference is invisible until an ancestor sets it and a descendant has a different font
/// size. Both halves were unmeasured until Acid1 was put through the corpus and came out with two
/// boxes a pixel too tall.
/// </remarks>
public class LineHeightTests
{
    [Test]
    [Arguments("line-height: 1", 16f)]
    [Arguments("line-height: 1.5", 24f)]
    [Arguments("line-height: 2", 32f)]
    [Arguments("line-height: 24px", 24f)]
    [Arguments("line-height: 150%", 24f)]
    [Arguments("font: 16px/1.5 \"Liberation Sans\"", 24f)]
    public async Task DeclaredOnTheElement(string css, float expected) =>
        await Assert.That(Height($"#x{{{css}}}", "<p id=\"x\">one line</p>")).IsEqualTo(expected);

    /// <summary>
    /// An ancestor's line-height reaches its descendants.
    /// </summary>
    /// <remarks>
    /// The regression this class exists for. The cascaded style carries no inherited values, and
    /// <c>line-height</c> was the one inherited property not re-applied from the parent — so it
    /// took effect on the element that declared it and on nothing inside. Since
    /// <c>body { line-height: … }</c> is how almost every stylesheet sets line spacing, the
    /// property did nothing in almost every real document.
    /// </remarks>
    [Test]
    [Arguments("line-height: 1", 16f)]
    [Arguments("line-height: 2", 32f)]
    [Arguments("line-height: 24px", 24f)]
    public async Task InheritedFromAnAncestor(string css, float expected) =>
        await Assert.That(Height($"#outer{{{css}}}", "<div id=\"outer\"><p id=\"x\">one line</p></div>"))
            .IsEqualTo(expected);

    /// <summary>
    /// A unitless value inherits as the NUMBER, and is re-resolved per descendant.
    /// </summary>
    /// <remarks>
    /// The reason the resolved pixels cannot simply be handed down. A number on an ancestor gives
    /// each descendant its own font size times that number, so 32px text under a 16px ancestor with
    /// <c>line-height: 1.5</c> gets 48px and not 24px. A length, by contrast, is inherited as the
    /// length and gives every descendant the same spacing whatever its size.
    /// </remarks>
    [Test]
    public async Task AUnitlessValueIsReresolvedAgainstEachFontSize()
    {
        var number = Height(
            "#outer{line-height: 1.5} #x{font-size: 32px}",
            "<div id=\"outer\"><p id=\"x\">one line</p></div>");

        await Assert.That(number).IsEqualTo(48);

        var length = Height(
            "#outer{line-height: 24px} #x{font-size: 32px}",
            "<div id=\"outer\"><p id=\"x\">one line</p></div>");

        await Assert.That(length).IsEqualTo(24);
    }

    /// <summary>
    /// An explicit <c>normal</c> stops the inheritance rather than continuing it.
    /// </summary>
    [Test]
    public async Task NormalReturnsToTheFontMetrics()
    {
        var inherited = Height(
            "#outer{line-height: 2}",
            "<div id=\"outer\"><p id=\"x\">one line</p></div>");

        var reset = Height(
            "#outer{line-height: 2} #x{line-height: normal}",
            "<div id=\"outer\"><p id=\"x\">one line</p></div>");

        await Assert.That(inherited).IsEqualTo(32);

        // Liberation Sans at 16px, with the metrics rounded to whole pixels before summing.
        await Assert.That(reset).IsEqualTo(18);
    }

    static float Height(string css, string body)
    {
        var html =
            "<!doctype html><html><head><style>" +
            "*{margin:0;padding:0;border:none}" +
            "html,body,div,p{margin:0;padding:0}" +
            "html{font-family:\"Liberation Sans\";font-size:16px}" +
            css +
            "</style></head><body>" + body + "</body></html>";

        var boxes = BoxDump.Measure(html, CorpusRunner.Options());
        return boxes[^1].Height;
    }
}
