public class BunitTestContext : BunitContext
{
    public BunitTestContext() =>
        // The app's own registrations, not a second set written to look like them. Registering
        // these by hand here is what let a lifetime bug reach a browser: the container the tests
        // built was not the container the app built, so it could not have caught one.
        //
        // Only the transport differs. FontStore fetches `fonts/<face>.ttf` and the sample button
        // fetches `sample/sample.html`; both are copied into the test output by the csproj, and
        // LocalAssetHandler serves them from there.
        Services.AddKrillaWeb(_ => new HttpClient(new LocalAssetHandler())
        {
            BaseAddress = new("http://localhost/")
        });
}
