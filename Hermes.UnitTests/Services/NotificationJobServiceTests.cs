using FluentResults;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Services.NotificationLogs;
using Hermes.Domain.ValueObjects;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

/// <summary>
/// Contains unit tests for <see cref="NotificationJobService"/>, verifying background job delegation
/// for news digest sending and email verification dispatches.
/// </summary>
public sealed class NotificationJobServiceTests
{
    /// <summary>
    /// Tests that <see cref="NotificationJobService.SendNewsDigestAsync"/> forwards the call to <see cref="INewsletterDigestService.SendAsync"/>.
    /// </summary>
    [Fact]
    public async Task SendNewsDigestAsync_Should_ForwardToNewsletterDigestService()
    {
        // Arrange
        Mock<INewsletterDigestService> digestMock = new();
        Mock<IVerificationDigestService> verificationMock = new();

        UserId userId = new(10);
        NewsletterId newsId = new(25);
        DateTime slotUtc = new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);

        digestMock.Setup(d => d.SendAsync(userId, newsId, slotUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(true));

        NotificationJobService sut = new(digestMock.Object, verificationMock.Object);

        // Act
        await sut.SendNewsDigestAsync(userId, newsId, slotUtc);

        // Assert
        digestMock.Verify(d => d.SendAsync(userId, newsId, slotUtc, It.IsAny<CancellationToken>()), Times.Once);
        verificationMock.Verify(v => v.SendAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="NotificationJobService.SendVerificationMailAsync"/> forwards the call to <see cref="IVerificationDigestService.SendAsync"/>.
    /// </summary>
    [Fact]
    public async Task SendVerificationMailAsync_Should_ForwardToVerificationDigestService()
    {
        // Arrange
        Mock<INewsletterDigestService> digestMock = new();
        Mock<IVerificationDigestService> verificationMock = new();

        UserId userId = new(42);
        verificationMock.Setup(v => v.SendAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(true));

        NotificationJobService sut = new(digestMock.Object, verificationMock.Object);

        // Act
        await sut.SendVerificationMailAsync(userId);

        // Assert
        verificationMock.Verify(v => v.SendAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        digestMock.Verify(d => d.SendAsync(It.IsAny<UserId>(), It.IsAny<NewsletterId>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that exceptions thrown by underlying services bubble up cleanly to allow Hangfire retry mechanics to activate.
    /// </summary>
    [Fact]
    public async Task SendNewsDigestAsync_Should_BubbleExceptions_ForHangfireRetries()
    {
        // Arrange
        Mock<INewsletterDigestService> digestMock = new();
        Mock<IVerificationDigestService> verificationMock = new();

        UserId userId = new(10);
        NewsletterId newsId = new(25);
        DateTime slotUtc = DateTime.UtcNow;

        digestMock.Setup(d => d.SendAsync(userId, newsId, slotUtc, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP connection timeout"));

        NotificationJobService sut = new(digestMock.Object, verificationMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SendNewsDigestAsync(userId, newsId, slotUtc));
    }
}
