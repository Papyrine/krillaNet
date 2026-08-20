/// <summary>
/// Checks on <see cref="FontSet"/>'s matching, and on the default family behind it.
///
/// None of this is reachable from the corpus: <c>Inputs/reset.css</c> pins
/// <c>font-family: "Liberation Sans"</c> on the root so that both engines load the same files, so
/// no scenario ever asks what happens when a document names no family. That is the right call for
/// the corpus and it leaves this whole path unmeasured, which is why these are here.
/// </summary>
public class FontSetTests
{
    [Test]
    public async Task AddDirectoryDefaultsToARegularUprightFace()
    {
        using var fonts = Load();

        // Not whichever file sorted first. In this directory that is LiberationMono-Bold, so a
        // document naming no family used to render entirely in bold.
        await Assert.That(fonts.Fallback!.Weight).IsEqualTo(400);
        await Assert.That(fonts.Fallback!.Italic).IsFalse();
    }

    [Test]
    public async Task AnExplicitFallbackIsNotTakenOver()
    {
        using var chosen = FontFace.LoadFile(
            Path.Combine(CorpusLayout.FontsDirectory, "LiberationSerif-Bold.ttf"));
        using var fonts = new FontSet();

        fonts.AddUnowned(chosen);
        fonts.Fallback = chosen;
        fonts.AddDirectory(CorpusLayout.FontsDirectory);

        await Assert.That(ReferenceEquals(fonts.Fallback, chosen)).IsTrue();
    }

    [Test]
    public async Task TheFallbackContributesItsFamilyRatherThanItself()
    {
        using var fonts = Load();

        fonts.Fallback = fonts.Resolve(["Liberation Serif"], 700, italic: false);

        // Nothing here names a family, so all three land on the fallback. Taking the face itself
        // would hand every one of them the bold upright it was set to, which is how an unstyled
        // <h1> came out unbolded and an <em> upright: not by resolving wrongly, but by never
        // resolving at all.
        var regular = fonts.Resolve([], 400, italic: false);
        var bold = fonts.Resolve([], 700, italic: false);
        var italic = fonts.Resolve([], 400, italic: true);

        await Assert.That(regular.Family).IsEqualTo("Liberation Serif");
        await Assert.That(regular.Weight).IsEqualTo(400);
        await Assert.That(bold.Weight).IsEqualTo(700);
        await Assert.That(italic.Italic).IsTrue();
    }

    [Test]
    public async Task AnUnstyledDocumentTakesTheGenericSerifFamily()
    {
        // Compared against the same document naming the family outright rather than against a
        // recorded width. What matters is that the default routes through the generic mapping,
        // and two conversions agreeing byte for byte says exactly that — while a document that
        // reached the fallback directly would agree with neither.
        var unstyled = Convert("<p>Hello</p>");
        var serif = Convert("""<p style="font-family: serif">Hello</p>""");
        var sans = Convert("""<p style="font-family: sans-serif">Hello</p>""");

        await Assert.That(unstyled.SequenceEqual(serif)).IsTrue();
        await Assert.That(unstyled.SequenceEqual(sans)).IsFalse();
    }

    /// <remarks>
    /// Its own set rather than <c>CorpusRunner</c>'s, which is shared across the whole run and
    /// binds no generics — every scenario names its families outright. Binding them on it here
    /// would reach every other test in the process.
    /// </remarks>
    static FontSet Load()
    {
        var fonts = new FontSet()
            .AddDirectory(CorpusLayout.FontsDirectory);

        fonts.Serif = "Liberation Serif";
        fonts.SansSerif = "Liberation Sans";
        fonts.Monospace = "Liberation Mono";

        return fonts;
    }

    static byte[] Convert(string body)
    {
        using var fonts = Load();

        var options = new HtmlOptions
        {
            PageWidth = CorpusLayout.PageWidth,
            PageHeight = CorpusLayout.PageHeight,
            Fonts = fonts
        };

        return HtmlConverter.Convert($"<!doctype html><html><body>{body}</body></html>", options);
    }
}
