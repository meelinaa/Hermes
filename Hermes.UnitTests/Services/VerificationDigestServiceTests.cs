using Hermes.Application.Models.Email;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Services;
using Hermes.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

public sealed class VerificationDigestServiceTests
{
    private static VerificationDigestService CreateSut(
        IHermesDataStore db,
        IEmailSender? emailSender = null)
    {
        var site = Options.Create(new HermesSiteUrlsOptions
        {
            PublicBaseUrl = "https://test.example",
            SupportEmail = "support@test.example",
        });
        return new VerificationDigestService(
            db,
            emailSender ?? Mock.Of<IEmailSender>(),
            site,
            NullLogger<VerificationDigestService>.Instance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SendAsync_Should_RejectNonPositiveUserId(int invalidId)
    {
        VerificationDigestService sut = CreateSut(Mock.Of<IHermesDataStore>());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.SendAsync(invalidId));
    }

    [Fact]
    public async Task SendAsync_Should_ReturnWithoutMail_WhenUserMissing()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.GetUserEntityByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Mock<IEmailSender> mail = new();
        VerificationDigestService sut = CreateSut(db.Object, mail.Object);

        await sut.SendAsync(3);

        mail.Verify(
            x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
        db.Verify(
            x => x.SetUserEmailVerificationChallengeAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_Should_ReturnWithoutMail_WhenUserHasNoEmail()
    {
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.GetUserEntityByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 3, Name = "N", Email = "  " });

        Mock<IEmailSender> mail = new();
        VerificationDigestService sut = CreateSut(db.Object, mail.Object);

        await sut.SendAsync(3);

        mail.Verify(
            x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_Should_PersistChallenge_AndSendMail_WhenUserValid()
    {
        User user = new() { Id = 10, Name = "Pat", Email = "pat@test.dev" };
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.GetUserEntityByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        string? capturedCode = null;
        db.Setup(x => x.SetUserEmailVerificationChallengeAsync(10, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<int, string, DateTime, CancellationToken>((_, code, _, _) => capturedCode = code)
            .Returns(Task.CompletedTask);

        Mock<IEmailSender> mail = new();
        mail.Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<EmailMessage, CancellationToken>((msg, _) =>
            {
                Assert.Equal("Hermes — Konto-Verifizierung", msg.Subject);
                Assert.Contains("pat@test.dev", msg.Body, StringComparison.OrdinalIgnoreCase);
            });

        VerificationDigestService sut = CreateSut(db.Object, mail.Object);

        await sut.SendAsync(10);

        Assert.NotNull(capturedCode);
        Assert.Equal(6, capturedCode!.Length);
        Assert.True(capturedCode.All(char.IsDigit));
        db.Verify(
            x => x.SetUserEmailVerificationChallengeAsync(10, capturedCode, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        mail.Verify(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_Should_Propagate_WhenSmtpFails()
    {
        User user = new() { Id = 1, Email = "e@test.dev", Name = "E" };
        Mock<IHermesDataStore> db = new();
        db.Setup(x => x.GetUserEntityByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        db.Setup(x => x.SetUserEmailVerificationChallengeAsync(1, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IEmailSender> mail = new();
        mail.Setup(x => x.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        VerificationDigestService sut = CreateSut(db.Object, mail.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SendAsync(1));
    }
}
