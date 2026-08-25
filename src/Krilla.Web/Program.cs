var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The fonts and the sample document are static web assets of this app, so everything the
// converter needs is fetched relative to wherever the app is served from. The registrations
// themselves live in ServiceRegistration so a test can build and validate the container; see the
// remarks there for why that is not over-engineering.
builder.Services.AddKrillaWeb(_ =>
    new()
    {
        BaseAddress = new(builder.HostEnvironment.BaseAddress)
    });

await builder.Build().RunAsync();
