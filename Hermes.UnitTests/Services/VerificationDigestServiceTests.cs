using System.Text.RegularExpressions;
using Hermes.Application.DTOs.Email;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Security;
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
        IUserRepository db,
        IEmailProvider? emailSender = null,
        IVerificationHtmlService? verificationRenderer = null,
        bool hashEmailVerificationCodes = true)
    {
        if (verificationRenderer is null)
        {
            Mock<IVerificationHtmlService> rendererMock = new();
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
            emailSender ?? Mock.Of<IEmailProvider>(),
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
        VerificationDigestService sut = CreateSut(Mock.Of<IUserRepository>());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.SendAsync(invalidId));
    }

    [Fact]
    public async Task SendAsync_Should_ReturnWithoutMail_WhenUserMissing()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Mock<IEmailProvider> mail = new();
        VerificationDigestService sut = CreateSut(db.Object, mail.Object);

        await sut.SendAsync(3);

        mail.Verify(
            emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
        db.Verify(
            dataStore => dataStore.SetUserEmailVerificationChallengeAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_Should_ReturnWithoutMail_WhenUserHasNoEmail()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 3, Name = "N", Email = "  " });

        Mock<IEmailProvider> mail = new();
        VerificationDigestService sut = CreateSut(db.Object, mail.Object);

        await sut.SendAsync(3);

        mail.Verify(
            emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SendAsync_Should_PersistChallenge_AndSendMail_WhenUserValid()
    {
        User user = new() { Id = 10, Name = "Pat", Email = "pat@test.dev" };
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        string? capturedCode = null;
        db.Setup(dataStore => dataStore.SetUserEmailVerificationChallengeAsync(10, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<int, string, DateTime, CancellationToken>((_, code, _, _) => capturedCode = code)
            .Returns(Task.CompletedTask);

        Mock<IEmailProvider> mail = new();
        mail.Setup(emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<EmailMessageDto, CancellationToken>((msg, _) =>
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
                It.Is<EmailMessageDto>(m => Regex.IsMatch(m.Body, @"\b\d{6}\b")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendAsync_Should_PersistPlainSixDigitCode_WhenHashingDisabled()
    {
        User user = new() { Id = 11, Name = "Pat", Email = "pat@test.dev" };
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(11, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        string? capturedCode = null;
        db.Setup(dataStore => dataStore.SetUserEmailVerificationChallengeAsync(11, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<int, string, DateTime, CancellationToken>((_, code, _, _) => capturedCode = code)
            .Returns(Task.CompletedTask);

        Mock<IEmailProvider> mail = new();
        mail.Setup(emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()))
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
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        db.Setup(dataStore => dataStore.SetUserEmailVerificationChallengeAsync(1, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        Mock<IEmailProvider> mail = new();
        mail.Setup(emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        VerificationDigestService sut = CreateSut(db.Object, mail.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SendAsync(1));
    }
}
