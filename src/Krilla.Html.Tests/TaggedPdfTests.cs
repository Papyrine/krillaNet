/// <summary>
/// The logical structure tree, which <see cref="HtmlOptions.Tagged"/> asks for.
/// </summary>
/// <remarks>
/// The corpus cannot measure this. A tag tree carries no ink, so the pixel comparison is blind to
/// it and the geometry comparison is too — the same position <c>link/</c> is in, and the same
/// answer: the PDF is read back and what it holds is asserted directly, and the corpus's own
/// numbers staying put is the separate check that adding the tree disturbed no layout.
/// </remarks>
public class TaggedPdfTests
{
    const string document =
        """
        <h1>The heading</h1>
        <p>A paragraph with <b>bold</b> in it.</p>
        <ul><li>One</li><li>Two</li></ul>
        <table>
          <caption>A table</caption>
          <tr><th scope="col">Head</th><td>Cell</td></tr>
        </table>
        <p><img src="../swatch.png" alt="A coloured swatch"></p>
        """;

    static HtmlOptions Options(bool tagged)
    {
        var options = CorpusRunner.Options(
            Path.Combine(CorpusLayout.InputsDirectory, "image", "inline_flow"));
        options.Tagged = tagged;
        return options;
    }

    /// <summary>
    /// The structure a representative document produces, read back out of the PDF.
    /// </summary>
    [Test]
    public async Task TheTreeIsBuiltFromTheDocument() =>
        await Verify(Structure(await HtmlConverter.ConvertAsync(document, Options(tagged: true))));

    /// <summary>
    /// Off by default, and off means no tree at all rather than an empty one.
    /// </summary>
    [Test]
    public async Task NothingIsTaggedByDefault()
    {
        var options = CorpusRunner.Options();

        await Assert.That(options.Tagged).IsFalse();

        var pdf = await HtmlConverter.ConvertAsync(document, options);

        await Assert.That(Structure(pdf)).IsEmpty();
    }

    /// <summary>
    /// Every corpus scenario converts with tagging on.
    /// </summary>
    /// <remarks>
    /// The breadth this feature needs and the snapshot above cannot give. krilla refuses an
    /// unbalanced marked-content span outright, so a page whose painting opens one and never closes
    /// it throws rather than producing a subtly wrong tree — which makes "converts at all" a real
    /// assertion over 150 documents holding floats, tables, page breaks, repeated headers, running
    /// margin boxes and generated content.
    /// </remarks>
    [Test]
    public async Task EveryScenarioConvertsTagged()
    {
        var failures = new List<string>();

        foreach (var directory in CorpusLayout.Directories())
        {
            var options = CorpusRunner.Options(directory);
            options.Tagged = true;

            try
            {
                var pdf = await HtmlConverter.ConvertAsync(CorpusLayout.Html(directory), options);

                if (pdf.Length == 0)
                {
                    failures.Add($"{CorpusLayout.Name(directory)}: produced nothing");
                }
            }
            catch (Exception exception)
            {
                failures.Add($"{CorpusLayout.Name(directory)}: {exception.Message}");
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    /// <summary>
    /// The document's language reaches the tree, which PDF/UA requires and which nothing on the
    /// page shows.
    /// </summary>
    [Test]
    public async Task TheLanguageReachesTheTree()
    {
        var pdf = await HtmlConverter.ConvertAsync(
            "<html lang=\"en-GB\"><body><p>Text</p></body></html>",
            Options(tagged: true));

        await Assert.That(Text(pdf)).Contains("/Lang");
    }

    /// <summary>
    /// The structure tags a PDF holds, in the order they appear in its objects.
    /// </summary>
    /// <remarks>
    /// Read out of the raw bytes rather than through PDFium, which exposes no structure tree at
    /// all. Crude, and sufficient: what is being asserted is which roles the document produced and
    /// how many of each, and a tag's name is the one thing that appears literally in the file.
    /// </remarks>
    static List<string> Structure(byte[] pdf)
    {
        var found = new List<string>();

        foreach (Match match in Regex.Matches(Text(pdf), @"/S\s*/(\w+)"))
        {
            found.Add(match.Groups[1].Value);
        }

        return found;
    }

    static string Text(byte[] pdf) =>
        Encoding.Latin1.GetString(pdf);
}
