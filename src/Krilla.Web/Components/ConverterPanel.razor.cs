using Microsoft.AspNetCore.Components.Forms;

namespace Krilla.Web.Components;

public partial class ConverterPanel :
    IAsyncDisposable
{
    // Enough markup to show the engine doing something worth watching — headings and an outline,
    // a table, a float, generated content — without needing a scrollbar to read.
    const string Starter =
        """
        <h1>Krilla</h1>
        <p>HTML to PDF, entirely in your browser.</p>
        <table>
          <tr><th>Engine</th><th>Runs in</th></tr>
          <tr><td>krilla</td><td>WebAssembly</td></tr>
        </table>
        """;

    string html = Starter;
    PaperSize paper = PaperSize.Letter;
    float margin = 48;
    bool busy;
    string? error;
    string? pdfUrl;
    ConversionResult? result;

    // Chrome's built-in PDF viewer reads Adobe's open parameters off the URL fragment. Left to
    // itself it wraps a preview a few inches wide in a full toolbar and a thumbnail sidebar, and
    // opens at whatever zoom fits the page height — which in a pane this shape is about 39%, small
    // enough that the point of a preview is lost. Asking for neither, fitted to the width, leaves
    // the page itself and nothing else.
    //
    // The fragment is kept OFF pdfUrl rather than baked into it, because that is the handle the
    // download and the revoke both use, and a blob URL carrying a fragment is a different string
    // from the one URL.revokeObjectURL was given.
    string? PreviewUrl =>
        pdfUrl is null ? null : $"{pdfUrl}#toolbar=0&navpanes=0&view=FitH";

    // A blob URL is a handle on bytes the browser is holding for us. Each conversion makes a new
    // one, so the previous has to be released or every convert leaks a PDF for the life of the
    // page — which on a page whose whole purpose is repeated conversion adds up quickly.
    async Task ReleasePdfAsync()
    {
        if (pdfUrl is null)
        {
            return;
        }

        await JSRuntime.InvokeVoidAsync("pdfBlob.release", pdfUrl);
        pdfUrl = null;
    }

    async Task ConvertAsync()
    {
        busy = true;
        error = null;
        await ReleasePdfAsync();
        result = null;

        try
        {
            var converted = await Conversion.ConvertAsync(html, paper, margin);
            result = converted;
            pdfUrl = await JSRuntime.InvokeAsync<string>("pdfBlob.create", converted.Pdf);
        }
        catch (Exception exception)
        {
            // Everything the engine can throw at a caller lands here: a document it cannot parse,
            // a page geometry that resolves to nothing, a font set that came back empty. The
            // message is the useful part, and burying it in the console would leave the page
            // looking like the button does nothing.
            error = exception.Message;
        }
        finally
        {
            busy = false;
        }
    }

    async Task LoadSampleAsync()
    {
        html = await Http.GetStringAsync("sample/sample.html");
        await ConvertAsync();
    }

    async Task LoadFileAsync(InputFileChangeEventArgs args)
    {
        var file = args.File;

        try
        {
            // A generous cap rather than none: the whole file is read into WebAssembly memory,
            // and an accidental drop of something enormous would take the tab down rather than
            // report anything.
            await using var stream = file.OpenReadStream(maxAllowedSize: 4 * 1024 * 1024);
            using var reader = new StreamReader(stream);
            html = await reader.ReadToEndAsync();
        }
        catch (IOException exception)
        {
            error = exception.Message;
            return;
        }

        await ConvertAsync();
    }

    async Task DownloadAsync() =>
        await JSRuntime.InvokeVoidAsync("pdfBlob.download", pdfUrl, "converted.pdf");

    static string FormatKb(int bytes) =>
        $"{bytes / 1024d:0.0} KB";

    public async ValueTask DisposeAsync() =>
        await ReleasePdfAsync();
}
