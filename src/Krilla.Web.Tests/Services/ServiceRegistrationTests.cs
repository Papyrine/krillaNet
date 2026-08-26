namespace Krilla.Web.Tests.Services;

/// <summary>
/// Builds the app's container the way the app does, with the validation the app only gets in
/// Development.
/// </summary>
/// <remarks>
/// This exists because of a bug it would have caught. FontStore and ConversionService were
/// registered as singletons over a scoped HttpClient, which the container refuses to construct —
/// and every test still passed, because bUnit registered its own services and the Playwright
/// tests serve the PUBLISHED app, which runs as Production where ValidateScopes is off. The
/// failure appeared only on `dotnet run`, which is Development.
///
/// So the validation is turned on explicitly here rather than inherited from an environment.
/// </remarks>
public class ServiceRegistrationTests
{
    static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        // Supplied by the Blazor host in the real app. Registered here so validation can reach
        // ThemePreferenceService rather than stopping at a missing dependency.
        services.AddScoped<IJSRuntime>(_ => new StubJSRuntime());

        services.AddKrillaWeb(_ =>
            new(new LocalAssetHandler())
            {
                BaseAddress = new("http://localhost/")
            });

        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
    }

    // ValidateOnBuild is what the app hits at startup: it constructs every descriptor and throws
    // on the first that cannot be satisfied. A lifetime mistake fails right here.
    [Test]
    public async Task ContainerIsValid()
    {
        await using var provider = Build();

        await Assert.That(provider).IsNotNull();
    }

    // ...and resolving through a scope is the other half. A singleton capturing a scoped
    // dependency is caught by ValidateScopes only when something actually asks for it.
    [Test]
    public async Task EveryServiceResolves()
    {
        await using var provider = Build();
        using var scope = provider.CreateScope();

        await Assert.That(scope.ServiceProvider.GetRequiredService<FontStore>()).IsNotNull();
        await Assert.That(scope.ServiceProvider.GetRequiredService<ConversionService>()).IsNotNull();
        await Assert.That(scope.ServiceProvider.GetRequiredService<ThemePreferenceService>()).IsNotNull();
    }

    class StubJSRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            default;

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            Cancel cancel,
            object?[]? args) =>
            default;
    }
}
