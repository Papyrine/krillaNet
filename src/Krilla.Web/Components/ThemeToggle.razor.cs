namespace Krilla.Web.Components;

public partial class ThemeToggle
{
    [Parameter]
    public ThemeType CurrentTheme { get; set; } = ThemeType.Light;

    [Parameter]
    public EventCallback<ThemeType> OnThemeChanged { get; set; }

    // The visible text is the target theme — "Dark" while the page is light — and a static
    // aria-label of "Toggle theme" REPLACED it for anyone using a screen reader, which is WCAG
    // 2.5.3 (Label in Name): the accessible name has to contain the text shown on the control.
    // It also lost the only thing the label says, namely which way the switch goes.
    string Label =>
        CurrentTheme == ThemeType.Light ? "Switch to dark theme" : "Switch to light theme";

    Task ToggleTheme()
    {
        var newTheme = CurrentTheme == ThemeType.Light ? ThemeType.Dark : ThemeType.Light;
        return OnThemeChanged.InvokeAsync(newTheme);
    }
}
