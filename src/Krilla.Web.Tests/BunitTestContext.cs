public class BunitTestContext : BunitContext
{
    public BunitTestContext()
    {
        // The converter's own services. The HttpClient is backed by the test output directory
        // rather than a real server: FontStore fetches `fonts/<face>.ttf` and the sample button
        // fetches `sample/sample.html`, and both are copied there by the csproj.
        Services.AddSingleton(_ => new HttpClient(new LocalAssetHandler())
        {
            BaseAddress = new("http://localhost/")
        });
        Services.AddSingleton<FontStore>();
        Services.AddSingleton<ConversionService>();
    }
}
