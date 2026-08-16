using Hermes.Application.DTOs.Email;
using Hermes.Notifications.Sending.HtmlLayout.Services;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Hermes.UnitTests.Notifications;

/// <summary>
/// Contains unit tests for <see cref="VerificationHtmlService"/> and <see cref="VerificationHtmlHelper"/>,
/// verifying verification code placement, date formatting, user greeting construction, and HTML encoding.
/// </summary>
public sealed class VerificationHtmlServiceTests
{
    /// <summary>
    /// Tests that <see cref="VerificationHtmlService.RenderVerificationAsync"/> throws <see cref="ArgumentNullException"/>
    /// when the request argument is null.
    /// </summary>
    [Fact]
    public async Task RenderVerificationAsync_Should_ThrowArgumentNullException_WhenRequestIsNull()
    {
        // Arrange
        VerificationHtmlService sut = new(TimeProvider.System);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.RenderVerificationAsync(null!));
    }

    /// <summary>
    /// Tests that the verification email renders the 6-digit code, custom German date, and personalized greeting.
    /// </summary>
    [Fact]
    public async Task RenderVerificationAsync_Should_RenderTemplateWithPersonalizedGreetingAndCode()
    {
        // Arrange
        DateTime fixedDate = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
        FakeTimeProvider timeProvider = new(fixedDate);
        VerificationHtmlService sut = new(timeProvider);

        VerificationRenderRequest request = new(
            UserDisplayName: "  Max Mustermann ",
            RecipientEmail: "max@example.org",
            VerificationCode: "481516",
            SupportEmail: "support@hermes.de",
            UnsubscribeUrl: "https://hermes.de/unsubscribe",
            SettingsUrl: "https://hermes.de/settings");

        // Act
        string html = await sut.RenderVerificationAsync(request);

        // Assert
        Assert.Contains("Hallo Max Mustermann,", html);
        Assert.Contains("16. August 2026", html);
        Assert.Contains("481516", html);
        Assert.Contains("support@hermes.de", html);
        Assert.Contains("max@example.org", html);
    }

    /// <summary>
    /// Tests that when <see cref="VerificationRenderRequest.UserDisplayName"/> is null or whitespace,
    /// the greeting renders as "Hallo,".
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenderVerificationAsync_Should_RenderGenericGreeting_WhenNameIsBlank(string? blankName)
    {
        // Arrange
        VerificationHtmlService sut = new(TimeProvider.System);

        VerificationRenderRequest request = new(
            UserDisplayName: blankName,
            RecipientEmail: "anon@example.org",
            VerificationCode: "123456",
            SupportEmail: "support@hermes.de",
            UnsubscribeUrl: "https://hermes.de/unsubscribe",
            SettingsUrl: "https://hermes.de/settings");

        // Act
        string html = await sut.RenderVerificationAsync(request);

        // Assert
        Assert.Contains("Hallo,", html);
    }

    /// <summary>
    /// Tests that HTML characters in user name or parameters are encoded properly.
    /// </summary>
    [Fact]
    public async Task RenderVerificationAsync_Should_HtmlEncodeUserValues()
    {
        // Arrange
        VerificationHtmlService sut = new(TimeProvider.System);

        VerificationRenderRequest request = new(
            UserDisplayName: "<script>alert(1)</script>",
            RecipientEmail: "user&test@example.org",
            VerificationCode: "999999",
            SupportEmail: "support@hermes.de",
            UnsubscribeUrl: "https://hermes.de/unsub?a=1&b=2",
            SettingsUrl: "https://hermes.de/settings");

        // Act
        string html = await sut.RenderVerificationAsync(request);

        // Assert
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("user&amp;test@example.org", html);
    }
}
