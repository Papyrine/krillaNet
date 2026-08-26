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

    [Test]
    public async Task Button_HasAriaLabel()
    {
        var cut = Render<ThemeToggle>(_ => _
            .Add(_ => _.CurrentTheme, ThemeType.Light));

        await Assert.That(cut.Find(".theme-toggle-btn").GetAttribute("aria-label")).IsEqualTo("Toggle theme");
    }
}
