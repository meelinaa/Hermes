using Bunit;
using Hermes.WebFrontend.Client.Components.UI;
using Hermes.WebFrontend.Client.Services.Theme;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace Hermes.WebFrontend.Client.Tests.Components.UI;

/// <summary>
/// bUnit tests verifying rendering, icon states, and theme toggle interactions in <see cref="ThemeToggle"/>.
/// </summary>
public sealed class ThemeToggleTests : BunitContext
{
    private readonly Mock<IJSRuntime> _jsMock = new();

    public ThemeToggleTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Renders_ThemeToggle_With_Tooltip_And_Icon()
    {
        // Arrange
        ThemeService themeService = new(_jsMock.Object);
        Services.AddSingleton<IThemeService>(themeService);

        // Act
        var cut = Render<ThemeToggle>(parameters => parameters
            .Add(p => p.ShowLabel, true));

        // Assert
        Assert.Contains("hermes-theme-toggle", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Design: System", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("System", cut.Find("span.hermes-theme-toggle__label").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void Clicking_ThemeToggle_Cycles_Theme_And_Updates_Markup()
    {
        // Arrange
        ThemeService themeService = new(_jsMock.Object);
        Services.AddSingleton<IThemeService>(themeService);

        var cut = Render<ThemeToggle>(parameters => parameters
            .Add(p => p.ShowLabel, true));

        // Act - Click toggle
        var button = cut.Find("button.hermes-theme-toggle");
        button.Click();

        // Assert
        Assert.Equal(AppTheme.Light, themeService.CurrentTheme);
        Assert.Contains("Design: Hell", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Hell", cut.Find("span.hermes-theme-toggle__label").TextContent, StringComparison.Ordinal);
    }
}
