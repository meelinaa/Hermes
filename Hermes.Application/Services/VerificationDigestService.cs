using System.Globalization;
using System.Security.Cryptography;
using Hermes.Application.DTOs.Email;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Security;
using Hermes.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Services;

/// <summary>
/// Generates cryptographically secure 6-digit OTP verification codes, persists active verification challenges (optionally hashed),
/// delegates template rendering to <see cref="IVerificationHtmlService"/>, and dispatches activation emails.
/// </summary>
public sealed class VerificationDigestService(
    IUserRepository users,
    IEmailProvider emailSender,
    IVerificationHtmlService verificationRenderer,
    IOptions<HermesSiteUrlsOptions> siteUrlsOptions,
    IOptions<SecurityOptions> securityOptions,
    ILogger<VerificationDigestService> logger) : IVerificationDigestService
{
    public const int VERIFICATION_CODE_VALIDITY_MINUTES = 15;

    /// <summary>
    /// Generates a cryptographically random 6-digit OTP, persists the active verification challenge (optionally hashed)
    /// with a 15-minute expiration window, renders the HTML email body, and sends the account activation message.
    /// </summary>
    /// <param name="userId">The unique identifier of the target user requesting email verification.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="userId"/> is less than or equal to zero.</exception>
    public async Task SendAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");

        User? user = await users.GetUserEntityByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
            return;

        string? code = GenerateNumericVerificationCode();
        DateTime expiresAt = DateTime.UtcNow.AddMinutes(VERIFICATION_CODE_VALIDITY_MINUTES);

        string persisted = securityOptions.Value.HashEmailVerificationCodes
            ? RefreshTokenHashService.Hash(code)
            : code;

        await users
            .SetUserEmailVerificationChallengeAsync(userId, persisted, expiresAt, cancellationToken)
            .ConfigureAwait(false);

        HermesSiteUrlsOptions site = siteUrlsOptions.Value;
        string? baseUrl = (site.PublicBaseUrl ?? "https://hermes.de").TrimEnd('/');
        string? supportEmail = (site.SupportEmail ?? "support@hermes.de").Trim();

        VerificationRenderRequest renderRequest = new(
            UserDisplayName: user.Name,
            RecipientEmail: user.Email.Trim(),
            VerificationCode: code,
            SupportEmail: supportEmail,
            UnsubscribeUrl: $"{baseUrl}/unsubscribe",
            SettingsUrl: $"{baseUrl}/settings");

        string body = await verificationRenderer
            .RenderVerificationAsync(renderRequest, cancellationToken)
            .ConfigureAwait(false);

        string? subject = $"Hermes — Konto-Verifizierung";

        try
        {
            await emailSender
                .SendAsync(
                    new EmailMessageDto(
                        new EmailRecipientDto(user.Email.Trim(), string.IsNullOrWhiteSpace(user.Name) ? null : user.Name),
                        subject,
                        body),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Verification e-mail sending for user {UserId} was canceled.", userId);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send verification e-mail for user {UserId}.", userId);
            throw;
        }
    }

    /// <summary>
    /// Generates a cryptographically random six-digit numeric code using <see cref="RandomNumberGenerator"/>
    /// to prevent predictable OTP challenge generation.
    /// </summary>
    /// <returns>A formatted 6-digit numeric string (padded with leading zeros if necessary).</returns>
    private static string GenerateNumericVerificationCode()
    {
        int randomNumber = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return randomNumber.ToString("D6", CultureInfo.InvariantCulture);
    }
}
