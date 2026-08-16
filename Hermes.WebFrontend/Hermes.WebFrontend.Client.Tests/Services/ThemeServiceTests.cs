using Hermes.WebFrontend.Client.Services.Theme;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace Hermes.WebFrontend.Client.Tests.Services;

/// <summary>
/// Unit tests verifying parsing, JS-Interop coordination, and theme cycling in <see cref="ThemeService"/>.
/// </summary>
public sealed class ThemeServiceTests
{
    private readonly Mock<IJSRuntime> _jsRuntimeMock = new();

    [Theory]
    [InlineData("light", AppTheme.Light)]
    [InlineData("LIGHT", AppTheme.Light)]
    [InlineData("dark", AppTheme.Dark)]
    [InlineData("DARK", AppTheme.Dark)]
    [InlineData("system", AppTheme.System)]
    [InlineData("unknown", AppTheme.System)]
    [InlineData(null, AppTheme.System)]
    public void ParseTheme_Should_Return_Expected_Theme(string? raw, AppTheme expected)
    {
        // Act
        AppTheme result = ThemeService.ParseTheme(raw);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task InitializeAsync_Should_Read_From_JsRuntime_And_Raise_ThemeChanged()
    {
        // Arrange
        _jsRuntimeMock.Setup(js => js.InvokeAsync<string>("hermesTheme.getTheme", It.IsAny<object[]>()))
            .ReturnsAsync("dark");

        ThemeService sut = new(_jsRuntimeMock.Object);
        bool eventFired = false;
        sut.ThemeChanged += () => eventFired = true;

        // Act
        await sut.InitializeAsync();

        // Assert
        Assert.Equal(AppTheme.Dark, sut.CurrentTheme);
        Assert.True(eventFired);
    }

    [Fact]
    public async Task SetThemeAsync_Should_Invoke_JsRuntime_And_Update_Theme()
    {
        // Arrange
        ThemeService sut = new(_jsRuntimeMock.Object);
        bool eventFired = false;
        sut.ThemeChanged += () => eventFired = true;

        // Act
        await sut.SetThemeAsync(AppTheme.Light);

        // Assert
        Assert.Equal(AppTheme.Light, sut.CurrentTheme);
        Assert.True(eventFired);
    }

    [Fact]
    public async Task CycleThemeAsync_Should_Rotate_Through_Themes_In_Sequence()
    {
        // Arrange
        ThemeService sut = new(_jsRuntimeMock.Object);
        Assert.Equal(AppTheme.System, sut.CurrentTheme);

        // Act & Assert 1: System -> Light
        await sut.CycleThemeAsync();
        Assert.Equal(AppTheme.Light, sut.CurrentTheme);

        // Act & Assert 2: Light -> Dark
        await sut.CycleThemeAsync();
        Assert.Equal(AppTheme.Dark, sut.CurrentTheme);

        // Act & Assert 3: Dark -> System
        await sut.CycleThemeAsync();
        Assert.Equal(AppTheme.System, sut.CurrentTheme);
    }
}
