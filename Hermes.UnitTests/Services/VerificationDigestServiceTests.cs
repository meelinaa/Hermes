using System.Text.RegularExpressions;
using Hermes.Application.DTOs.Email;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Security;
using Hermes.Application.Ports.Inbound;
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
        IUserStore db,
        IEmailSender? emailSender = null,
        IVerificationRenderer? verificationRenderer = null,
        bool hashEmailVerificationCodes = true)
    {
        if (verificationRenderer is null)
        {
            Mock<IVerificationRenderer> rendererMock = new();
            rendererMock
                .Setup(r => r.RenderVerificationAsync(It.IsAny<VerificationRenderRequest>(), It.IsAny<CancellationToken>()))
                .Returns<VerificationRenderRequest, CancellationToken>((req, _) =>
                    Task.FromResult($"<html>code={req.VerificationCode} email={req.RecipientEmail}</html>"));
            verificationRenderer = rendererMock.Object;
        }

        IOptions<HermesSiteUrlsOptions> site = Options.Create(new HermesSiteUrlsOptions
        {
            PublicBaseUrl = "https://test.example",
            SupportEmail = "support@test.example",
        });
        IOptions<SecurityOptions> security = Options.Create(new SecurityOptions
        {
            HashEmailVerificationCodes = hashEmailVerificationCodes,
        });
        return new VerificationDigestService(
            db,
            emailSender ?? Mock.Of<IEmailSender>(),
            verificationRenderer,
            site,
            security,
            NullLogger<VerificationDigestService>.Instance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SendAsync_Should_RejectNonPositiveUserId(int invalidId)
    {
        VerificationDigestService sut = CreateSut(Mock.Of<IUserStore>());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.SendAsync(invalidId));
    }

    [Fact]
    public async Task SendAsync_Should_ReturnWithoutMail_WhenUserMissing()
    {
        Mock<IUserStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Mock<IEmailSender> mail = new();
        VerificationDigestService sut = CreateSut(db.Object, mail.Object);

        await sut.SendAsync(3);

        mail.Verify(
            emailSender => emailSender.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
        db.Verify(
            dataStore => dataStore.SetUserEmailVerificationChallengeAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_Should_ReturnWithoutMail_WhenUserHasNoEmail()
    {
        Mock<IUserStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 3, Name = "N", Email = "  " });

        Mock<IEmailSender> mail = new();
        VerificationDigestService sut = CreateSut(db.Object, mail.Object);

        await sut.SendAsync(3);

        mail.Verify(
            emailSender => emailSender.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_Should_PersistChallenge_AndSendMail_WhenUserValid()
    {
        User user = new() { Id = 10, Name = "Pat", Email = "pat@test.dev" };
        Mock<IUserStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        string? capturedCode = null;
        db.Setup(dataStore => dataStore.SetUserEmailVerificationChallengeAsync(10, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<int, string, DateTime, CancellationToken>((_, code, _, _) => capturedCode = code)
            .Returns(Task.CompletedTask);

        Mock<IEmailSender> mail = new();
        mail.Setup(emailSender => emailSender.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<EmailMessage, CancellationToken>((msg, _) =>
            {
                Assert.Equal("Hermes — Konto-Verifizierung", msg.Subject);
                Assert.Contains("pat@test.dev", msg.Body, StringComparison.OrdinalIgnoreCase);
            });

        VerificationDigestService sut = CreateSut(db.Object, mail.Object);

        await sut.SendAsync(10);

        Assert.NotNull(capturedCode);
        Assert.Matches("^[0-9A-F]{64}$", capturedCode!);
        db.Verify(
            dataStore => dataStore.SetUserEmailVerificationChallengeAsync(10, capturedCode!, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        mail.Verify(
            emailSender => emailSender.SendAsync(
                It.Is<EmailMessage>(m => Regex.IsMatch(m.Body, @"\b\d{6}\b")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_Should_PersistPlainSixDigitCode_WhenHashingDisabled()
    {
        User user = new() { Id = 11, Name = "Pat", Email = "pat@test.dev" };
        Mock<IUserStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        string? capturedCode = null;
        db.Setup(dataStore => dataStore.SetUserEmailVerificationChallengeAsync(11, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<int, string, DateTime, CancellationToken>((_, code, _, _) => capturedCode = code)
            .Returns(Task.CompletedTask);

        Mock<IEmailSender> mail = new();
        mail.Setup(emailSender => emailSender.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        VerificationDigestService sut = CreateSut(db.Object, mail.Object, hashEmailVerificationCodes: false);

        await sut.SendAsync(11);

        Assert.NotNull(capturedCode);
        Assert.Equal(6, capturedCode!.Length);
        Assert.True(capturedCode.All(char.IsDigit));
    }

    [Fact]
    public async Task SendAsync_Should_Propagate_WhenSmtpFails()
    {
        User user = new() { Id = 1, Email = "e@test.dev", Name = "E" };
        Mock<IUserStore> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        db.Setup(dataStore => dataStore.SetUserEmailVerificationChallengeAsync(1, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IEmailSender> mail = new();
        mail.Setup(emailSender => emailSender.SendAsync(It.IsAny<EmailMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        VerificationDigestService sut = CreateSut(db.Object, mail.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SendAsync(1));
    }
}
