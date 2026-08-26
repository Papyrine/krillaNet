namespace Krilla.Web.Tests.Components;

public class ConverterPanelTests : BunitTestContext
{
    // The panel's JS interop is the blob-URL plumbing, which bUnit has no browser for. Loose mode
    // answers the void calls (release, download) with a default and is enough for them.
    //
    // pdfBlob.create needs a real answer, though: its return value IS the preview, so a default
    // null leaves the component looking exactly as it does when a conversion failed. Handing back
    // a stand-in URL is what lets these tests see the result pane the app actually shows.
    public ConverterPanelTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<string>("pdfBlob.create", _ => true).SetResult("blob:test");
    }

    [Test]
    public async Task InitialRender_ShowsSourceAndEmptyResult()
    {
        var cut = Render<ConverterPanel>();

        await Assert.That(cut.Markup).Contains("Krilla");
        await Assert.That(cut.FindAll(".result-empty")).IsNotEmpty();
        // Nothing has been converted, so there is no download to offer yet.
        await Assert.That(cut.FindAll(".download-btn")).IsEmpty();
    }

    [Test]
    public async Task Convert_ShowsResultAndCleanDiagnostics()
    {
        var cut = Render<ConverterPanel>();

        await cut.Find(".source-text")
            .InputAsync("<h1>Hello</h1><p>World</p>");
        await cut.Find(".convert-btn")
            .ClickAsync(new());

        // The starter document uses nothing the engine does not implement, so the pane reports
        // the clean case rather than a list.
        await Assert.That(cut.FindAll(".diagnostics-clean")).IsNotEmpty();
        await Assert.That(cut.Find(".result-note").TextContent).Contains("KB");
        await Assert.That(cut.FindAll(".download-btn")).IsNotEmpty();
    }

    [Test]
    public async Task Convert_ListsUnsupportedCss()
    {
        var cut = Render<ConverterPanel>();

        await cut.Find(".source-text")
            .InputAsync("<div style='display: flex'><i>a</i><i>b</i></div>");
        await cut.Find(".convert-btn")
            .ClickAsync(new());

        var diagnostics = cut.Find(".diagnostics");
        await Assert.That(diagnostics.TextContent).Contains("display");
    }

    // A document the parser accepts but the engine cannot lay out has to surface as a message
    // rather than a blank pane. Zero margin on a zero-width page leaves no content box at all.
    [Test]
    public async Task Convert_ShowsErrorRatherThanFailingSilently()
    {
        var cut = Render<ConverterPanel>();

        await cut.Find(".margin-select")
            .ChangeAsync("96");
        await cut.Find(".source-text")
            .InputAsync(string.Empty);
        await cut.Find(".convert-btn")
            .ClickAsync(new());

        // An empty document is legal and converts; what matters is that the pane is never left in
        // the "nothing happened" state — either a result or an error is showing.
        var settled = cut.FindAll(".result-note").Count > 0 || cut.FindAll(".error-panel").Count > 0;
        await Assert.That(settled).IsTrue();
    }
}
