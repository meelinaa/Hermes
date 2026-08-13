using System.Globalization;
using System.Security.Cryptography;

using Microsoft.Extensions.Logging;
using Hermes.Application.Logging;
using Microsoft.Extensions.Options;

using Hermes.Application.DTOs.Email;
using Hermes.Application.Options.Auth;
using Hermes.Application.Options.External;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Security;
using Hermes.Domain.Entities;

namespace Hermes.Application.Services.Users;

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
    /// <summary>
    /// The validity duration of a generated verification OTP code in minutes (15 minutes).
    /// </summary>
    public const int VERIFICATION_CODE_VALIDITY_MINUTES = 15;

    private const string DefaultPublicBaseUrl = "https://hermes.de";
    private const string DefaultSupportEmail = "support@hermes.de";
    private const string SettingsEndpointPath = "/settings";
    private const string UnsubscribeEndpointPath = "/unsubscribe";
    private const string VerificationEmailSubject = "Hermes — Konto-Verifizierung";
    private const int MaxOtpValueExclusive = 1_000_000;

    private readonly IEmailProvider _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
    private readonly ILogger<VerificationDigestService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IOptions<SecurityOptions> _securityOptions = securityOptions ?? throw new ArgumentNullException(nameof(securityOptions));
    private readonly IOptions<HermesSiteUrlsOptions> _siteUrlsOptions = siteUrlsOptions ?? throw new ArgumentNullException(nameof(siteUrlsOptions));
    private readonly IUserRepository _users = users ?? throw new ArgumentNullException(nameof(users));
    private readonly IVerificationHtmlService _verificationRenderer = verificationRenderer ?? throw new ArgumentNullException(nameof(verificationRenderer));

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

        User? user = await _users.GetUserEntityByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
            return;

        string? code = GenerateNumericVerificationCode();
        DateTime expiresAt = DateTime.UtcNow.AddMinutes(VERIFICATION_CODE_VALIDITY_MINUTES);

        string persisted = _securityOptions.Value.HashEmailVerificationCodes
            ? RefreshTokenHashUtility.Hash(code)
            : code;

        await _users
            .SetUserEmailVerificationChallengeAsync(userId, persisted, expiresAt, cancellationToken)
            .ConfigureAwait(false);

        HermesSiteUrlsOptions site = _siteUrlsOptions.Value;
        string? baseUrl = (site.PublicBaseUrl ?? DefaultPublicBaseUrl).TrimEnd('/');
        string? supportEmail = (site.SupportEmail ?? DefaultSupportEmail).Trim();

        VerificationRenderRequest renderRequest = new(
            UserDisplayName: user.Name,
            RecipientEmail: user.Email.Trim(),
            VerificationCode: code,
            SupportEmail: supportEmail,
            UnsubscribeUrl: $"{baseUrl}{UnsubscribeEndpointPath}",
            SettingsUrl: $"{baseUrl}{SettingsEndpointPath}");

        string emailBody = await _verificationRenderer
            .RenderVerificationAsync(renderRequest, cancellationToken)
            .ConfigureAwait(false);

        string emailSubject = VerificationEmailSubject;

        try
        {
            await _emailSender
                .SendAsync(
                    new EmailMessageDto(
                        new EmailRecipientDto(user.Email.Trim(), string.IsNullOrWhiteSpace(user.Name) ? null : user.Name),
                        emailSubject,
                        emailBody),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogVerificationCanceled(userId);
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogVerificationFailed(exception, userId);
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
        int randomNumber = RandomNumberGenerator.GetInt32(0, MaxOtpValueExclusive);
        return randomNumber.ToString("D6", CultureInfo.InvariantCulture);
    }
}
