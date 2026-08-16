using Hermes.WebFrontend.Client.Services.Notifications;
using Xunit;

namespace Hermes.WebFrontend.Client.Tests.Services;

/// <summary>
/// Unit tests verifying message dispatching, severity levels, and dismissal in <see cref="ToastNotificationService"/>.
/// </summary>
public sealed class ToastNotificationServiceTests
{
    [Fact]
    public void Show_Should_AddToastMessage_AndRaiseOnChange()
    {
        // Arrange
        using ToastNotificationService sut = new();
        bool eventFired = false;
        sut.OnChange += () => eventFired = true;

        // Act
        sut.Show("Test notification", "Header", ToastNotificationLevel.Info, 0);

        // Assert
        Assert.True(eventFired);
        Assert.Single(sut.Toasts);
        Assert.Equal("Test notification", sut.Toasts[0].Message);
        Assert.Equal("Header", sut.Toasts[0].Title);
        Assert.Equal(ToastNotificationLevel.Info, sut.Toasts[0].Level);
    }

    [Theory]
    [InlineData(ToastNotificationLevel.Success)]
    [InlineData(ToastNotificationLevel.Error)]
    [InlineData(ToastNotificationLevel.Warning)]
    [InlineData(ToastNotificationLevel.Info)]
    public void SpecificShowMethods_Should_SetCorrectLevel(ToastNotificationLevel level)
    {
        // Arrange
        using ToastNotificationService sut = new();

        // Act
        switch (level)
        {
            case ToastNotificationLevel.Success:
                sut.ShowSuccess("Success msg", "Title", 0);
                break;
            case ToastNotificationLevel.Error:
                sut.ShowError("Error msg", "Title", 0);
                break;
            case ToastNotificationLevel.Warning:
                sut.ShowWarning("Warning msg", "Title", 0);
                break;
            case ToastNotificationLevel.Info:
                sut.ShowInfo("Info msg", "Title", 0);
                break;
        }

        // Assert
        Assert.Single(sut.Toasts);
        Assert.Equal(level, sut.Toasts[0].Level);
    }

    [Fact]
    public void Dismiss_Should_RemoveSpecificToast()
    {
        // Arrange
        using ToastNotificationService sut = new();
        sut.Show("First message", null, ToastNotificationLevel.Info, 0);
        sut.Show("Second message", null, ToastNotificationLevel.Warning, 0);
        Guid firstId = sut.Toasts[0].Id;
        Guid secondId = sut.Toasts[1].Id;

        // Act
        sut.Dismiss(firstId);

        // Assert
        Assert.Single(sut.Toasts);
        Assert.Equal(secondId, sut.Toasts[0].Id);
        Assert.Equal("Second message", sut.Toasts[0].Message);
    }

    [Fact]
    public void Show_BlankMessage_Should_BeIgnored()
    {
        // Arrange
        using ToastNotificationService sut = new();

        // Act
        sut.Show("   ");

        // Assert
        Assert.Empty(sut.Toasts);
    }
}
