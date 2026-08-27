namespace Krilla.Web.Services;

public enum ThemeType
{
    Light,
    Dark
}

public class ThemePreferenceService(IJSRuntime jsRuntime)
{
    const string ThemeKey = "selectedTheme";

    /// <summary>
    /// The reader's chosen theme, falling back to whatever their system asks for.
    /// </summary>
    /// <remarks>
    /// The fallback is the point. A first visit has nothing saved, and answering that with Light
    /// regardless ignores a preference the reader has already expressed to their operating system.
    /// <c>themeManager.initializeTheme</c> resolves the same answer the same way before Blazor
    /// boots, so the pre-boot paint and this agree and there is no flash between them.
    /// </remarks>
    public async Task<ThemeType> GetSavedThemeAsync()
    {
        var value = await jsRuntime.InvokeAsync<string?>("statePreference.get", ThemeKey);
        if (value is not null && Enum.TryParse<ThemeType>(value, out var theme))
        {
            return theme;
        }

        var preferred = await jsRuntime.InvokeAsync<string?>("themeManager.preferredTheme");
        if (preferred is not null && Enum.TryParse<ThemeType>(preferred, out var system))
        {
            return system;
        }

        return ThemeType.Light;
    }

    public async Task SaveThemeAsync(ThemeType theme) =>
        await jsRuntime.InvokeVoidAsync("statePreference.set", ThemeKey, theme.ToString());
}
