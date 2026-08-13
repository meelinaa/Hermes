using System.Text.RegularExpressions;
using Hermes.Application.DTOs.Email;
using Hermes.Application.Options.Auth;
using Hermes.Application.Options.External;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Security;
using Hermes.Application.Services.Users;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;
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
            TimeProvider.System,
            NullLogger<VerificationDigestService>.Instance);
    }

    // [B]OUNDARY: Rejects non-positive user ID input parameter
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SendAsync_Should_RejectNonPositiveUserId(int invalidId)
    {
        // Arrange
        VerificationDigestService sut = CreateSut(Mock.Of<IUserRepository>());

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await sut.SendAsync(new UserId(invalidId)));
    }

    // [B]OUNDARY: Aborts early without sending email or setting challenge when user is missing
    [Fact]
    public async Task SendAsync_Should_ReturnWithoutMail_WhenUserMissing()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(3), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        Mock<IEmailProvider> mail = new();
        VerificationDigestService sut = CreateSut(db.Object, mail.Object);

        // Act
        await sut.SendAsync(new UserId(3));

        // Assert
        mail.Verify(
            emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
        db.Verify(
            dataStore => dataStore.SetUserEmailVerificationChallengeAsync(It.IsAny<UserId>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // [B]OUNDARY: Aborts early without sending email when target user has blank email address
    [Fact]
    public async Task SendAsync_Should_ReturnWithoutMail_WhenUserHasNoEmail()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(3), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = new UserId(3), Name = "N", Email = "  " });

        Mock<IEmailProvider> mail = new();
        VerificationDigestService sut = CreateSut(db.Object, mail.Object);

        // Act
        await sut.SendAsync(new UserId(3));

        // Assert
        mail.Verify(
            emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // [R]IGHT: Generates 6-digit OTP code, persists hashed challenge, and dispatches verification email
    [Fact]
    public async Task SendAsync_Should_PersistChallenge_AndSendMail_WhenUserValid()
    {
        // Arrange
        User user = new() { Id = new UserId(10), Name = "Pat", Email = "pat@test.dev" };
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(10), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        string? capturedCode = null;
        db.Setup(dataStore => dataStore.SetUserEmailVerificationChallengeAsync(new UserId(10), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<UserId, string, DateTime, CancellationToken>((_, code, _, _) => capturedCode = code)
            .Returns(ValueTask.CompletedTask);

        Mock<IEmailProvider> mail = new();
        mail.Setup(emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<EmailMessageDto, CancellationToken>((msg, _) =>
            {
                Assert.Equal("Hermes — Konto-Verifizierung", msg.Subject);
                Assert.Contains("pat@test.dev", msg.Body, StringComparison.OrdinalIgnoreCase);
            });

        VerificationDigestService sut = CreateSut(db.Object, mail.Object);

        // Act
        await sut.SendAsync(new UserId(10));

        // Assert
        Assert.NotNull(capturedCode);
        Assert.Matches("^[0-9A-F]{64}$", capturedCode!);
        db.Verify(
            dataStore => dataStore.SetUserEmailVerificationChallengeAsync(new UserId(10), capturedCode!, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
        mail.Verify(
            emailSender => emailSender.SendAsync(
                It.Is<EmailMessageDto>(m => Regex.IsMatch(m.Body, @"\b\d{6}\b")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // [R]IGHT: Persists plain 6-digit challenge code when code hashing option is disabled
    [Fact]
    public async Task SendAsync_Should_PersistPlainSixDigitCode_WhenHashingDisabled()
    {
        // Arrange
        User user = new() { Id = new UserId(11), Name = "Pat", Email = "pat@test.dev" };
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(11), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        string? capturedCode = null;
        db.Setup(dataStore => dataStore.SetUserEmailVerificationChallengeAsync(new UserId(11), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<UserId, string, DateTime, CancellationToken>((_, code, _, _) => capturedCode = code)
            .Returns(ValueTask.CompletedTask);

        Mock<IEmailProvider> mail = new();
        mail.Setup(emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        VerificationDigestService sut = CreateSut(db.Object, mail.Object, hashEmailVerificationCodes: false);

        // Act
        await sut.SendAsync(new UserId(11));

        // Assert
        Assert.NotNull(capturedCode);
        Assert.Equal(6, capturedCode!.Length);
        Assert.True(capturedCode.All(char.IsDigit));
    }

    // [E]RROR: Propagates exception when underlying email provider fails to send
    [Fact]
    public async Task SendAsync_Should_Propagate_WhenSmtpFails()
    {
        // Arrange
        User user = new() { Id = new UserId(1), Email = "e@test.dev", Name = "E" };
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(1), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        db.Setup(dataStore => dataStore.SetUserEmailVerificationChallengeAsync(new UserId(1), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        Mock<IEmailProvider> mail = new();
        mail.Setup(emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        VerificationDigestService sut = CreateSut(db.Object, mail.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await sut.SendAsync(new UserId(1)));
    }
}
