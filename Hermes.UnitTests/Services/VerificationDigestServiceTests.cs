using System.Text.RegularExpressions;
using FluentResults;
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
using Microsoft.Extensions.Logging;

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
            SupportEmail = Email.Parse("support@test.example"),
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
            TimeProvider.System);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SendAsync_Should_Fail_WhenUserIdNotPositive(int invalidId)
    {
        var sut = CreateSut(Mock.Of<IUserRepository>());
        var result = await sut.SendAsync(new UserId(invalidId));
        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task SendAsync_Should_ReturnFalse_WhenUserMissing()
    {
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(3), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        Mock<IEmailProvider> mail = new();
        var sut = CreateSut(db.Object, mail.Object);

        var result = await sut.SendAsync(new UserId(3));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
        mail.Verify(emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_Should_PersistChallenge_AndSendMail_WhenUserValid()
    {
        User user = new() { Id = new UserId(10), Name = "Pat", Email = Email.Parse("pat@test.dev") };
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

        var sut = CreateSut(db.Object, mail.Object);

        var result = await sut.SendAsync(new UserId(10));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.NotNull(capturedCode);
        Assert.Matches("^[0-9A-F]{64}$", capturedCode!);
        db.Verify(dataStore => dataStore.SetUserEmailVerificationChallengeAsync(new UserId(10), capturedCode!, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        mail.Verify(emailSender => emailSender.SendAsync(It.Is<EmailMessageDto>(m => Regex.IsMatch(m.Body, @"\b\d{6}\b")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_Should_PersistPlainSixDigitCode_WhenHashingDisabled()
    {
        User user = new() { Id = new UserId(11), Name = "Pat", Email = Email.Parse("pat@test.dev") };
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(11), It.IsAny<CancellationToken>())).ReturnsAsync(user);
        string? capturedCode = null;
        db.Setup(dataStore => dataStore.SetUserEmailVerificationChallengeAsync(new UserId(11), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<UserId, string, DateTime, CancellationToken>((_, code, _, _) => capturedCode = code)
            .Returns(ValueTask.CompletedTask);

        Mock<IEmailProvider> mail = new();
        mail.Setup(emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(db.Object, mail.Object, hashEmailVerificationCodes: false);

        var result = await sut.SendAsync(new UserId(11));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.NotNull(capturedCode);
        Assert.Equal(6, capturedCode!.Length);
        Assert.True(capturedCode.All(char.IsDigit));
    }
}

public sealed class VerificationDigestLoggingDecoratorTests
{
    [Fact]
    public async Task SendAsync_Should_LogFailed_WhenInnerFails()
    {
        Mock<IVerificationDigestService> inner = new();
        inner.Setup(i => i.SendAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Fail("SMTP down"));
        var sut = new VerificationDigestLoggingDecorator(inner.Object, Mock.Of<ILogger<VerificationDigestLoggingDecorator>>());

        var result = await sut.SendAsync(new UserId(1));

        Assert.True(result.IsFailed);
        Assert.Equal("SMTP down", result.Errors[0].Message);
    }

    [Fact]
    public async Task SendAsync_Should_LogAndThrow_WhenInnerThrows()
    {
        Mock<IVerificationDigestService> inner = new();
        inner.Setup(i => i.SendAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("SMTP down"));
        var sut = new VerificationDigestLoggingDecorator(inner.Object, Mock.Of<ILogger<VerificationDigestLoggingDecorator>>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SendAsync(new UserId(1)));
    }
}

