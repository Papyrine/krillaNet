namespace Krilla.Web.Services;

/// <summary>
/// The app's service registrations, in one place so a test can validate them.
/// </summary>
/// <remarks>
/// <para>
/// Extracted from <c>Program.cs</c> deliberately. Registering these inline meant the only thing
/// that ever built the container was the app itself, and the app only fails on a bad lifetime
/// when scope validation is on — which it is in Development and is NOT in Production. So a
/// container that could not be constructed at all still passed every test here, because the
/// Playwright tests serve the PUBLISHED output and a published Blazor app runs as Production.
/// </para>
/// <para>
/// Everything is scoped, matching the <c>HttpClient</c>. In a WebAssembly app there is exactly
/// one scope for the life of the page, so scoped and singleton have the same lifetime in
/// practice — but only one of them is a lifetime the container will accept, since a singleton
/// may not capture a scoped dependency.
/// </para>
/// </remarks>
public static class ServiceRegistration
{
    /// <summary>
    /// Registers the converter's services against a caller-supplied <see cref="HttpClient"/>.
    /// </summary>
    /// <remarks>
    /// The client is a parameter because the desktop tests have no server to fetch the fonts and
    /// the sample document from, and swap the transport rather than the code above it.
    /// </remarks>
    public static IServiceCollection AddKrillaWeb(
        this IServiceCollection services,
        Func<IServiceProvider, HttpClient> client)
    {
        services.AddScoped(client);
        services.AddScoped<FontStore>();
        services.AddScoped<ConversionService>();
        services.AddScoped<ThemePreferenceService>();
        return services;
    }
}
