namespace Krilla.Web.Tests.Components;

public class ThemeToggleTests : BunitTestContext
{
    [Test]
    public async Task InitialRender_WithLightTheme_ShowsDarkButton()
    {
        var cut = Render<ThemeToggle>(_ => _
            .Add(_ => _.CurrentTheme, ThemeType.Light));

        var button = cut.Find(".theme-toggle-btn");
        await Assert.That(button.TextContent).Contains("Dark");
    }

    [Test]
    public async Task InitialRender_WithDarkTheme_ShowsLightButton()
    {
        var cut = Render<ThemeToggle>(_ => _
            .Add(_ => _.CurrentTheme, ThemeType.Dark));

        var button = cut.Find(".theme-toggle-btn");
        await Assert.That(button.TextContent).Contains("Light");
    }

    [Test]
    public async Task ClickButton_WithLightTheme_InvokesDarkTheme()
    {
        ThemeType? newTheme = null;
        var cut = Render<ThemeToggle>(_ => _
            .Add(_ => _.CurrentTheme, ThemeType.Light)
            .Add(_ => _.OnThemeChanged, (ThemeType theme) => newTheme = theme));

        await cut.Find(".theme-toggle-btn").ClickAsync(new());

        await Assert.That(newTheme).IsEqualTo(ThemeType.Dark);
    }

    // The accessible name has to CONTAIN the visible text, which is WCAG 2.5.3 (Label in Name) —
    // a voice-control user says what they can see. A static "Toggle theme" replaced the visible
    // "Dark"/"Light" instead, so both themes are asserted here rather than one: a label that does
    // not follow the state is exactly the regression that reintroduces the failure.
    [Test]
    [Arguments(ThemeType.Light, "Dark", "Switch to dark theme")]
    [Arguments(ThemeType.Dark, "Light", "Switch to light theme")]
    public async Task Button_AriaLabelContainsVisibleText(ThemeType theme, string visible, string expected)
    {
        var cut = Render<ThemeToggle>(_ => _
            .Add(_ => _.CurrentTheme, theme));

        var button = cut.Find(".theme-toggle-btn");

        await Assert.That(button.GetAttribute("aria-label")).IsEqualTo(expected);
        await Assert.That(button.TextContent).Contains(visible);
        await Assert.That(button.GetAttribute("aria-label")!.ToLowerInvariant()).Contains(visible.ToLowerInvariant());
    }
}
