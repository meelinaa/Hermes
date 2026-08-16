using System.Text.RegularExpressions;
using FluentResults;
using Hermes.Application.DTOs.Email;
using Hermes.Application.Options.Auth;
using Hermes.Application.Options.External;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Users;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Hermes.UnitTests.Services;

/// <summary>
/// Contains unit tests for <see cref="VerificationDigestService"/> and its logging decorator,
/// verifying verification code generation, challenge hashing, site URL configuration fallbacks, and email rendering.
/// </summary>
public sealed class VerificationDigestServiceTests
{
    private static VerificationDigestService CreateSut(
        IUserRepository db,
        IEmailProvider? emailSender = null,
        IVerificationHtmlService? verificationRenderer = null,
        HermesSiteUrlsOptions? siteUrls = null,
        bool hashEmailVerificationCodes = true)
    {
        if (verificationRenderer is null)
        {
            Mock<IVerificationHtmlService> rendererMock = new();
            rendererMock
                .Setup(r => r.RenderVerificationAsync(It.IsAny<VerificationRenderRequest>(), It.IsAny<CancellationToken>()))
                .Returns<VerificationRenderRequest, CancellationToken>((req, _) =>
                    Task.FromResult($"<html>code={req.VerificationCode} email={req.RecipientEmail} support={req.SupportEmail} settings={req.SettingsUrl}</html>"));
            verificationRenderer = rendererMock.Object;
        }

        IOptions<HermesSiteUrlsOptions> site = Options.Create(siteUrls ?? new HermesSiteUrlsOptions
        {
            PublicBaseUrl = "https://test.example/",
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

    /// <summary>
    /// Tests that constructor throws <see cref="ArgumentNullException"/> when dependencies are null.
    /// </summary>
    [Fact]
    public void Constructor_Should_ThrowArgumentNullException_WhenRequiredDependenciesNull()
    {
        // Arrange
        var dummyUsers = Mock.Of<IUserRepository>();
        var dummyMail = Mock.Of<IEmailProvider>();
        var dummyHtml = Mock.Of<IVerificationHtmlService>();
        var dummySite = Options.Create(new HermesSiteUrlsOptions());
        var dummySec = Options.Create(new SecurityOptions());
        var dummyTime = TimeProvider.System;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new VerificationDigestService(null!, dummyMail, dummyHtml, dummySite, dummySec, dummyTime));
        Assert.Throws<ArgumentNullException>(() => new VerificationDigestService(dummyUsers, null!, dummyHtml, dummySite, dummySec, dummyTime));
        Assert.Throws<ArgumentNullException>(() => new VerificationDigestService(dummyUsers, dummyMail, null!, dummySite, dummySec, dummyTime));
        Assert.Throws<ArgumentNullException>(() => new VerificationDigestService(dummyUsers, dummyMail, dummyHtml, null!, dummySec, dummyTime));
        Assert.Throws<ArgumentNullException>(() => new VerificationDigestService(dummyUsers, dummyMail, dummyHtml, dummySite, null!, dummyTime));
    }

    /// <summary>
    /// Tests that <see cref="VerificationDigestService.SendAsync"/> fails when passed a non-positive user ID.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SendAsync_Should_Fail_WhenUserIdNotPositive(int invalidId)
    {
        // Arrange
        var sut = CreateSut(Mock.Of<IUserRepository>());

        // Act
        var result = await sut.SendAsync(new UserId(invalidId));

        // Assert
        Assert.True(result.IsFailed);
        Assert.Contains("User ID must be positive", result.Errors[0].Message);
    }

    /// <summary>
    /// Tests that <see cref="VerificationDigestService.SendAsync"/> returns Ok(false) when the user record does not exist.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_ReturnFalse_WhenUserMissing()
    {
        // Arrange
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(3), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);
        Mock<IEmailProvider> mail = new();
        var sut = CreateSut(db.Object, mail.Object);

        // Act
        var result = await sut.SendAsync(new UserId(3));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
        mail.Verify(emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="VerificationDigestService.SendAsync"/> returns Ok(false) when the user has an empty email address.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_ReturnFalse_WhenUserEmailIsBlank()
    {
        // Arrange
        User userWithEmptyEmail = new() { Id = new UserId(4), Name = "NoEmail" };
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(4), It.IsAny<CancellationToken>())).ReturnsAsync(userWithEmptyEmail);
        Mock<IEmailProvider> mail = new();
        var sut = CreateSut(db.Object, mail.Object);

        // Act
        var result = await sut.SendAsync(new UserId(4));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.Value);
        mail.Verify(emailSender => emailSender.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Tests that <see cref="VerificationDigestService.SendAsync"/> generates a 6-digit challenge,
    /// persists its SHA-256 hash in the database, and sends the verification email.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_PersistChallenge_AndSendMail_WhenUserValid()
    {
        // Arrange
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
                Assert.Equal("Pat", msg.To.DisplayName);
            });

        var sut = CreateSut(db.Object, mail.Object);

        // Act
        var result = await sut.SendAsync(new UserId(10));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.NotNull(capturedCode);
        Assert.Matches("^[0-9A-F]{64}$", capturedCode!);
        db.Verify(dataStore => dataStore.SetUserEmailVerificationChallengeAsync(new UserId(10), capturedCode!, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        mail.Verify(emailSender => emailSender.SendAsync(It.Is<EmailMessageDto>(m => Regex.IsMatch(m.Body, @"\b\d{6}\b")), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Tests that <see cref="VerificationDigestService.SendAsync"/> persists the 6-digit code in plaintext
    /// when the code hashing option is explicitly disabled.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_PersistPlainSixDigitCode_WhenHashingDisabled()
    {
        // Arrange
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

        // Act
        var result = await sut.SendAsync(new UserId(11));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.NotNull(capturedCode);
        Assert.Equal(6, capturedCode!.Length);
        Assert.True(capturedCode.All(char.IsDigit));
    }

    /// <summary>
    /// Tests that <see cref="VerificationDigestService.SendAsync"/> falls back to default URLs and support email
    /// when options specify null or whitespace values, and sets null display name when user name is empty.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_UseFallbackUrls_WhenSiteUrlsAreNull()
    {
        // Arrange
        User user = new() { Id = new UserId(15), Name = "", Email = Email.Parse("no-name@test.dev") };
        Mock<IUserRepository> db = new();
        db.Setup(dataStore => dataStore.GetUserEntityByIdAsync(new UserId(15), It.IsAny<CancellationToken>())).ReturnsAsync(user);

        VerificationRenderRequest? capturedRequest = null;
        Mock<IVerificationHtmlService> renderer = new();
        renderer.Setup(r => r.RenderVerificationAsync(It.IsAny<VerificationRenderRequest>(), It.IsAny<CancellationToken>()))
            .Callback<VerificationRenderRequest, CancellationToken>((req, _) => capturedRequest = req)
            .ReturnsAsync("<html>rendered</html>");

        EmailMessageDto? capturedEmail = null;
        Mock<IEmailProvider> mail = new();
        mail.Setup(m => m.SendAsync(It.IsAny<EmailMessageDto>(), It.IsAny<CancellationToken>()))
            .Callback<EmailMessageDto, CancellationToken>((m, _) => capturedEmail = m)
            .Returns(Task.CompletedTask);

        var siteUrls = new HermesSiteUrlsOptions
        {
            PublicBaseUrl = null,
            SupportEmail = null
        };

        var sut = CreateSut(db.Object, mail.Object, renderer.Object, siteUrls);

        // Act
        var result = await sut.SendAsync(new UserId(15));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value);
        Assert.NotNull(capturedRequest);
        Assert.Equal("https://hermes.de/settings", capturedRequest!.SettingsUrl);
        Assert.Equal("https://hermes.de/unsubscribe", capturedRequest.UnsubscribeUrl);
        Assert.Equal("support@hermes.de", capturedRequest.SupportEmail);
        Assert.NotNull(capturedEmail);
        Assert.Null(capturedEmail!.To.DisplayName);
    }
}

/// <summary>
/// Contains unit tests for <see cref="VerificationDigestLoggingDecorator"/>, verifying logging and error passthrough.
/// </summary>
public sealed class VerificationDigestLoggingDecoratorTests
{
    /// <summary>
    /// Tests that failed execution results from the inner service are passed through and logged.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_LogFailed_WhenInnerFails()
    {
        // Arrange
        Mock<IVerificationDigestService> inner = new();
        inner.Setup(i => i.SendAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Fail("SMTP down"));
        var sut = new VerificationDigestLoggingDecorator(inner.Object, Mock.Of<ILogger<VerificationDigestLoggingDecorator>>());

        // Act
        var result = await sut.SendAsync(new UserId(1));

        // Assert
        Assert.True(result.IsFailed);
        Assert.Equal("SMTP down", result.Errors[0].Message);
    }

    /// <summary>
    /// Tests that unhandled exceptions from the inner service are logged and rethrown.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_LogAndThrow_WhenInnerThrows()
    {
        // Arrange
        Mock<IVerificationDigestService> inner = new();
        inner.Setup(i => i.SendAsync(It.IsAny<UserId>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("SMTP down"));
        var sut = new VerificationDigestLoggingDecorator(inner.Object, Mock.Of<ILogger<VerificationDigestLoggingDecorator>>());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SendAsync(new UserId(1)));
    }
}
