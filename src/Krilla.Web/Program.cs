var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The fonts and the sample document are static web assets of this app, so everything the
// converter needs is fetched relative to wherever the app is served from.
builder.Services
    .AddScoped(_ =>
        new HttpClient
        {
            BaseAddress = new(builder.HostEnvironment.BaseAddress)
        });

// Singletons rather than scoped: a FontSet owns native font handles and is expensive to build,
// and in a WebAssembly app there is exactly one user, so the distinction costs nothing and the
// faces are downloaded and parsed once for the life of the page.
builder.Services.AddSingleton<FontStore>();
builder.Services.AddSingleton<ConversionService>();
builder.Services.AddScoped<ThemePreferenceService>();

await builder.Build().RunAsync();
