using Hermes.WebFrontend.Client.Services.Notifications;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace Hermes.WebFrontend.Client.Tests.Services;

public sealed class WebPushNotificationServiceTests
{
    [Fact]
    public async Task GetPermissionStatusAsync_ReturnsStatus_FromJs()
    {
        // Arrange
        var mockJs = new Mock<IJSRuntime>();
        mockJs.Setup(js => js.InvokeAsync<string>("hermesPush.getPermission", It.IsAny<object?[]>()))
            .ReturnsAsync("granted");

        var service = new WebPushNotificationService(mockJs.Object);

        // Act
        string status = await service.GetPermissionStatusAsync();

        // Assert
        Assert.Equal("granted", status);
    }

    [Fact]
    public async Task GetPermissionStatusAsync_ReturnsUnsupported_WhenJsFails()
    {
        // Arrange
        var mockJs = new Mock<IJSRuntime>();
        mockJs.Setup(js => js.InvokeAsync<string>("hermesPush.getPermission", It.IsAny<object?[]>()))
            .ThrowsAsync(new JSException("unsupported"));

        var service = new WebPushNotificationService(mockJs.Object);

        // Act
        string status = await service.GetPermissionStatusAsync();

        // Assert
        Assert.Equal("unsupported", status);
    }

    [Fact]
    public async Task RequestPermissionAsync_ReturnsPermission_FromJs()
    {
        // Arrange
        var mockJs = new Mock<IJSRuntime>();
        mockJs.Setup(js => js.InvokeAsync<string>("hermesPush.requestPermission", It.IsAny<object?[]>()))
            .ReturnsAsync("granted");

        var service = new WebPushNotificationService(mockJs.Object);

        // Act
        string status = await service.RequestPermissionAsync();

        // Assert
        Assert.Equal("granted", status);
    }

    [Fact]
    public async Task RequestPermissionAsync_ReturnsDenied_WhenJsFails()
    {
        // Arrange
        var mockJs = new Mock<IJSRuntime>();
        mockJs.Setup(js => js.InvokeAsync<string>("hermesPush.requestPermission", It.IsAny<object?[]>()))
            .ThrowsAsync(new JSException("denied"));

        var service = new WebPushNotificationService(mockJs.Object);

        // Act
        string status = await service.RequestPermissionAsync();

        // Assert
        Assert.Equal("denied", status);
    }

    [Fact]
    public async Task SendNotificationAsync_ReturnsTrue_WhenJsSucceeds()
    {
        // Arrange
        var mockJs = new Mock<IJSRuntime>();
        mockJs.Setup(js => js.InvokeAsync<bool>("hermesPush.sendNotification", It.IsAny<object?[]>()))
            .ReturnsAsync(true);

        var service = new WebPushNotificationService(mockJs.Object);

        // Act
        bool result = await service.SendNotificationAsync("Breaking News", "Headline details");

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendNotificationAsync_ReturnsFalse_WhenTitleIsNullOrWhitespace(string? title)
    {
        // Arrange
        var mockJs = new Mock<IJSRuntime>();
        var service = new WebPushNotificationService(mockJs.Object);

        // Act
        bool result = await service.SendNotificationAsync(title!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendNotificationAsync_ReturnsFalse_WhenJsFails()
    {
        // Arrange
        var mockJs = new Mock<IJSRuntime>();
        mockJs.Setup(js => js.InvokeAsync<bool>("hermesPush.sendNotification", It.IsAny<object?[]>()))
            .ThrowsAsync(new JSException("error"));

        var service = new WebPushNotificationService(mockJs.Object);

        // Act
        bool result = await service.SendNotificationAsync("Title", "Body");

        // Assert
        Assert.False(result);
    }
}
