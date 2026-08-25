/// <summary>
/// Serves the app's static web assets out of the test output directory.
/// </summary>
/// <remarks>
/// The app fetches its fonts and sample document over HTTP because in a browser there is no
/// other way to read them. A desktop test has no server, so rather than teach the code a second
/// way to load them — which would leave the real path untested — the transport is replaced and
/// everything above it stays exactly as it ships.
/// </remarks>
public class LocalAssetHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var relative = request.RequestUri!.AbsolutePath.TrimStart('/');

        // `fonts/X.ttf` and `sample/sample.html` are both flattened into the output directory by
        // the csproj, so only the file name is used to find them.
        var path = Path.Combine(
            AppContext.BaseDirectory,
            relative.StartsWith("fonts/") ? relative : Path.GetFileName(relative));

        if (!File.Exists(path))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        return Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(File.ReadAllBytes(path))
            });
    }
}
