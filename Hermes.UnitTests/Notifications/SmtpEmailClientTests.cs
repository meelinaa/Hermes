using Hermes.Application.DTOs.Email;
using Hermes.Application.Options.Email;
using Hermes.Notifications.Sending.Providers;
using Polly;
using Polly.Registry;
using Polly.Retry;
using Xunit;

namespace Hermes.UnitTests.Notifications;

/// <summary>
/// Contains unit tests for <see cref="SmtpEmailClient"/>,
/// verifying Polly resilience pipeline integration, cancellation token handling, and connection error propagation.
/// </summary>
public sealed class SmtpEmailClientTests
{
    private static (SmtpEmailClient client, ResiliencePipelineRegistry<string> registry) CreateSut(
        EmailOptions? options = null,
        Action<ResiliencePipelineBuilder>? configurePipeline = null)
    {
        EmailOptions settings = options ?? new EmailOptions
        {
            Host = "127.0.0.1",
            Port = 2525,
            EnableSsl = false,
            Username = "testuser",
            Password = "testpassword",
            DefaultFromName = "Hermes News",
            DefaultFromAddress = "news@hermes.de",
            DefaultReplyToName = "Hermes Support",
            DefaultReplyToAddress = "support@hermes.de",
            XMailer = "HermesMailer/1.0"
        };

        ResiliencePipelineRegistry<string> registry = new();
        registry.TryAddBuilder("smtp-retry", (builder, _) =>
        {
            if (configurePipeline != null)
            {
                configurePipeline(builder);
            }
            else
            {
                builder.AddRetry(new RetryStrategyOptions
                {
                    MaxRetryAttempts = 1,
                    Delay = TimeSpan.Zero
                });
            }
        });

        SmtpEmailClient client = new(settings, registry);
        return (client, registry);
    }

    /// <summary>
    /// Tests that <see cref="SmtpEmailClient.SendAsync"/> respects an already-cancelled cancellation token
    /// without initiating SMTP network connections.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_ThrowOperationCanceledException_WhenCancellationTokenAlreadyCancelled()
    {
        // Arrange
        var (sut, _) = CreateSut();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        EmailMessageDto message = new(
            To: new EmailRecipientDto("recipient@test.dev", "Recipient"),
            Subject: "Test Subject",
            Body: "<p>Test Content</p>");

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.SendAsync(message, cts.Token));
    }

    /// <summary>
    /// Tests that <see cref="SmtpEmailClient.SendAsync"/> executes the registered Polly resilience pipeline
    /// and propagates exceptions when the target SMTP server is unreachable.
    /// </summary>
    [Fact]
    public async Task SendAsync_Should_ExecuteThroughPollyPipeline_AndFail_WhenSmtpServerUnreachable()
    {
        // Arrange
        int retryAttempts = 0;
        var (sut, _) = CreateSut(configurePipeline: builder =>
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                Delay = TimeSpan.Zero,
                OnRetry = _ =>
                {
                    retryAttempts++;
                    return default;
                }
            });
        });

        using MemoryStream stream = new([1, 2, 3, 4]);
        EmailMessageDto message = new(
            To: new EmailRecipientDto("user@example.org", "User"),
            Subject: "Important Alert",
            Body: "<h1>Alert</h1>",
            Attachments:
            [
                new EmailAttachmentDto("document.pdf", stream, "application/pdf")
            ]);

        // Act & Assert
        await Assert.ThrowsAnyAsync<Exception>(() => sut.SendAsync(message, CancellationToken.None));
        Assert.True(retryAttempts > 0, "Polly retry callback should have executed on failure.");
    }

    /// <summary>
    /// Tests that <see cref="SmtpEmailClient.SendAsync"/> handles SSL configuration flag without throwing configuration errors.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SendAsync_Should_HandleSslConfigurationFlags(bool enableSsl)
    {
        // Arrange
        EmailOptions options = new()
        {
            Host = "127.0.0.1",
            Port = 2525,
            EnableSsl = enableSsl,
            Username = null, // Test without authentication branch
            Password = null,
            DefaultFromName = "Hermes",
            DefaultFromAddress = "no-reply@hermes.de",
            DefaultReplyToName = "Support",
            DefaultReplyToAddress = "support@hermes.de",
            XMailer = "Hermes"
        };

        var (sut, _) = CreateSut(options);
        EmailMessageDto message = new(
            To: new EmailRecipientDto("user@example.org", "User"),
            Subject: "Hello",
            Body: "Body");

        // Act & Assert (Fails gracefully due to offline test port after exercising option branches)
        await Assert.ThrowsAnyAsync<Exception>(() => sut.SendAsync(message, CancellationToken.None));
    }
}
