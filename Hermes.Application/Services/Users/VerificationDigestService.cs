using System.Globalization;
using System.Security.Cryptography;
using FluentResults;
using Microsoft.Extensions.Options;

using Hermes.Application.DTOs.Email;
using Hermes.Application.Options.Auth;
using Hermes.Application.Options.External;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Security;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Services.Users;

public sealed class VerificationDigestService(
    IUserRepository users,
    IEmailProvider emailSender,
    IVerificationHtmlService verificationRenderer,
    IOptions<HermesSiteUrlsOptions> siteUrlsOptions,
    IOptions<SecurityOptions> securityOptions,
    TimeProvider timeProvider) : IVerificationDigestService
{
    public const int VERIFICATION_CODE_VALIDITY_MINUTES = 15;

    private const string DefaultPublicBaseUrl = "https://hermes.de";
    private const string DefaultSupportEmail = "support@hermes.de";
    private const string SettingsEndpointPath = "/settings";
    private const string UnsubscribeEndpointPath = "/unsubscribe";
    private const string VerificationEmailSubject = "Hermes — Konto-Verifizierung";
    private const int MaxOtpValueExclusive = 1_000_000;

    private readonly IEmailProvider _emailSender = emailSender ?? throw new ArgumentNullException(nameof(emailSender));
    private readonly IOptions<SecurityOptions> _securityOptions = securityOptions ?? throw new ArgumentNullException(nameof(securityOptions));
    private readonly IOptions<HermesSiteUrlsOptions> _siteUrlsOptions = siteUrlsOptions ?? throw new ArgumentNullException(nameof(siteUrlsOptions));
    private readonly IUserRepository _users = users ?? throw new ArgumentNullException(nameof(users));
    private readonly IVerificationHtmlService _verificationRenderer = verificationRenderer ?? throw new ArgumentNullException(nameof(verificationRenderer));

    public async Task<Result<bool>> SendAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        if (userId.Value <= 0)
            return Result.Fail("User ID must be positive.");

        User? user = await _users.GetUserEntityByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || string.IsNullOrWhiteSpace(user.Email.Value))
            return Result.Ok(false);

        string? code = GenerateNumericVerificationCode();
        DateTime expiresAt = timeProvider.GetUtcNow().UtcDateTime.AddMinutes(VERIFICATION_CODE_VALIDITY_MINUTES);

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
            RecipientEmail: user.Email!.Value.Trim(),
            VerificationCode: code,
            SupportEmail: supportEmail,
            UnsubscribeUrl: $"{baseUrl}{UnsubscribeEndpointPath}",
            SettingsUrl: $"{baseUrl}{SettingsEndpointPath}");

        string emailBody = await _verificationRenderer
            .RenderVerificationAsync(renderRequest, cancellationToken)
            .ConfigureAwait(false);

        string emailSubject = VerificationEmailSubject;

        await _emailSender
            .SendAsync(
                new EmailMessageDto(
                    new EmailRecipientDto(user.Email!.Value.Trim(), string.IsNullOrWhiteSpace(user.Name) ? null : user.Name),
                    emailSubject,
                    emailBody),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Ok(true);
    }

    private static string GenerateNumericVerificationCode()
    {
        int randomNumber = RandomNumberGenerator.GetInt32(0, MaxOtpValueExclusive);
        return randomNumber.ToString("D6", CultureInfo.InvariantCulture);
    }
}
