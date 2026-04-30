using Hermes.Application.Jobs;
using Hermes.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Worker.Jobs;

public sealed class NotificationJobsTests
{
    [Fact]
    public async Task SendNewsDigestAsync_Should_DelegateToDigestService()
    {
        Mock<INewsletterDigestService> digest = new();
        Mock<IVerificationDigestService> verify = new();
        digest.Setup(x => x.SendAsync(7, 3, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        NotificationJobs sut = new(digest.Object, verify.Object, NullLogger<NotificationJobs>.Instance);

        var slot = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        await sut.SendNewsDigestAsync(7, 3, slot);

        digest.Verify(x => x.SendAsync(7, 3, slot, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendVerificationMailAsync_Should_DelegateToVerificationDigestService()
    {
        Mock<INewsletterDigestService> digest = new();
        Mock<IVerificationDigestService> verify = new();
        verify.Setup(x => x.SendAsync(99, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        NotificationJobs sut = new(digest.Object, verify.Object, NullLogger<NotificationJobs>.Instance);

        await sut.SendVerificationMailAsync(99);

        verify.Verify(x => x.SendAsync(99, It.IsAny<CancellationToken>()), Times.Once);
    }
}
