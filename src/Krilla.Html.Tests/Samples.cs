/// <summary>
/// Tests that double as the readme's usage snippets, via MarkdownSnippets.
/// </summary>
/// <remarks>
/// The same arrangement as Krilla.Tests' Samples, minus the snapshot: a conversion here is a whole
/// PDF of paginated text, and pinning its bytes would make every layout change in the engine a
/// readme baseline to regenerate. The corpus already measures fidelity, so what these have to
/// establish is only that the code the readme shows compiles and does what the prose beside it
/// says.
/// </remarks>
public class Samples
{
    [Test]
    public async Task HtmlToPdf()
    {
        // The readme reads "fonts"; the test needs the faces the rest of the suite loads.
        var fontDirectory = CorpusLayout.FontsDirectory;

        #region HtmlToPdf

        using var fonts = new FontSet()
            .AddDirectory(fontDirectory);

        var pdf = HtmlConverter.Convert(
            "<h1>Hello</h1><p>World</p>",
            new()
            {
                Fonts = fonts
            });

        #endregion

        using var document = PdfiumDocument.Load(pdf);
        await Assert.That(document.PageCount).IsEqualTo(1);
    }

    [Test]
    public async Task ImagePolicies()
    {
        var assetDirectory = CorpusLayout.InputsDirectory;
        var options = CorpusRunner.Options();

        var asked = new List<string>();
        options.ImageResolver = source =>
        {
            asked.Add(source);
            return null;
        };

        #region ImagePolicies

        options.LocalImages = ImagePolicy.SafeDirectories(assetDirectory);
        options.WebImages = ImagePolicy.SafeDomains("cdn.example.com");

        #endregion

        HtmlConverter.Convert(
            """
            <img src="https://cdn.example.com/logo.png">
            <img src="https://elsewhere.example/logo.png">
            """,
            options);

        // The permitted host reaches the resolver and the other never does, which is the whole
        // point of the policy sitting outside the resolver rather than inside it.
        await Assert.That(asked.Count).IsEqualTo(1);
        await Assert.That(asked[0]).IsEqualTo("https://cdn.example.com/logo.png");
    }

    [Test]
    public async Task Diagnostics()
    {
        var options = CorpusRunner.Options();

        #region Diagnostics

        options.OnDiagnostic = diagnostic => Console.WriteLine(diagnostic);

        // <div> display: flex — laid out as a block
        // <table> border-collapse: collapse — laid out with the separated border model
        // <p> align: left — not applied, because presentational attributes are not mapped onto the cascade
        // <img> src: logo.png — did not resolve to an image, so no box was generated

        #endregion

        // The four lines above are produced rather than transcribed. The sink is reassigned after
        // the region so the readme can show the Console.WriteLine a reader would actually write,
        // while the comments beside it stay pinned to what the converter really reports.
        var reported = new List<string>();
        options.OnDiagnostic = diagnostic => reported.Add(diagnostic.ToString());

        HtmlConverter.Convert(
            """
            <div style="display: flex">Flexed</div>
            <table style="border-collapse: collapse">
              <tr><td>Cell</td></tr>
            </table>
            <p align="left">Aligned</p>
            <img src="logo.png">
            """,
            options);

        await Assert.That(reported).IsEquivalentTo(
        [
            "<div> display: flex — laid out as a block",
            "<table> border-collapse: collapse — laid out with the separated border model",
            "<p> align: left — not applied, because presentational attributes are not mapped onto the cascade",
            "<img> src: logo.png — did not resolve to an image, so no box was generated"
        ]);
    }
}
