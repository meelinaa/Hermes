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
/// Generates a six-digit verification code, persists it for the user,
/// renders the verification HTML via <see cref="IVerificationRenderer"/>,
/// and delivers the e-mail. Rendering is delegated to an injected renderer
/// so the Application layer stays free of HTML/template concerns.
/// </summary>
public sealed class VerificationDigestService(
    IUserRepository users,
    IEmailSender emailSender,
    IVerificationRenderer verificationRenderer,
    IOptions<HermesSiteUrlsOptions> siteUrlsOptions,
    IOptions<SecurityOptions> securityOptions,
    ILogger<VerificationDigestService> logger) : IVerificationDigestService
{
    public const int VERIFICATION_CODE_VALIDITY_MINUTES = 15;

    /// <summary>
    /// Sends a verification e-mail containing a six-digit code to the user
    /// identified by <paramref name="userId"/>. The code is persisted
    /// (optionally hashed) before the e-mail is dispatched.
    /// </summary>
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
            ? RefreshTokenHasher.Hash(code)
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
                    new EmailMessage(
                        new EmailRecipient(user.Email.Trim(), string.IsNullOrWhiteSpace(user.Name) ? null : user.Name),
                        subject,
                        body),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send verification e-mail for user {UserId}.", userId);
            throw;
        }
    }

    /// <summary>
    /// Generates a cryptographically random six-digit numeric code
    /// used for e-mail verification challenges.
    /// </summary>
    private static string GenerateNumericVerificationCode()
    {
        int randomNumber = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return randomNumber.ToString("D6", CultureInfo.InvariantCulture);
    }
}
