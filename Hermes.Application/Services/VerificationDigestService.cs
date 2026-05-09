using System.Globalization;
using System.Security.Cryptography;
using Hermes.Application.Models.Email;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Domain.Entities;
using Hermes.Notifications.Sending.HtmlLayout;
using Hermes.Notifications.Sending.HtmlLayout.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Services;

/// <summary>
/// Persists a time-bound verification code on the user and sends the HTML verification e-mail (see <c>Verification.html</c>).
/// </summary>
public sealed class VerificationDigestService(
    IHermesDataStore dataStore,
    IEmailSender emailSender,
    IOptions<HermesSiteUrlsOptions> siteUrlsOptions,
    ILogger<VerificationDigestService> logger) : IVerificationDigestService
{
    public const int VERIFICATION_CODE_VALIDITY_MINUTES = 15;
    private static readonly CultureInfo _digestCulture = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Generates and stores a verification challenge, then sends the verification e-mail.</summary>
    public async Task SendAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");

        User? user = await dataStore.GetUserEntityByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
            return;

        string? code = GenerateNumericVerificationCode();
        DateTime expiresAt = DateTime.UtcNow.AddMinutes(VERIFICATION_CODE_VALIDITY_MINUTES);

        await dataStore
            .SetUserEmailVerificationChallengeAsync(userId, code, expiresAt, cancellationToken)
            .ConfigureAwait(false);

        HermesSiteUrlsOptions site = siteUrlsOptions.Value;
        string? baseUrl = (site.PublicBaseUrl ?? "https://hermes.de").TrimEnd('/');
        string? supportEmail = (site.SupportEmail ?? "support@hermes.de").Trim();
        string? body = await BuildVerificationBodyAsync(
                user.Name,
                user.Email.Trim(),
                code,
                supportEmail,
                $"{baseUrl}/unsubscribe",
                $"{baseUrl}/settings",
                cancellationToken)
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

    /// <summary>Creates a cryptographically secure six-digit numeric verification code.</summary>
    private static string GenerateNumericVerificationCode()
    {
        int randomNumber = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return randomNumber.ToString("D6", CultureInfo.InvariantCulture);
    }

    /// <summary>Builds the verification HTML body with user greeting, code, and footer links.</summary>
    private static async Task<string> BuildVerificationBodyAsync(
        string? userDisplayName,
        string recipientEmail,
        string verificationCode,
        string supportEmail,
        string deaboUrl,
        string settingsUrl,
        CancellationToken cancellationToken)
    {
        string? dateDisplay = DateTime.UtcNow.ToString("dd. MMMM yyyy", _digestCulture);

        string? intro = string.IsNullOrWhiteSpace(userDisplayName)
            ? "Hallo,"
            : $"Hallo {userDisplayName.Trim()},";

        const string INTRO_2 =
            "Vielen Dank für Ihre Registrierung bei Hermes. Um Ihr Konto zu verifizieren, verwenden Sie bitte den folgenden Verifizierungscode:";

        string? infoFooter = $"Diese E-Mail wurde an {recipientEmail} gesendet";

        VerificationContent content = new(
            Header: "Hermes",
            Header2: "Konto-Verifizierung",
            DateDisplay: dateDisplay,
            Intro: intro,
            Intro2: INTRO_2,
            VerificationCode: verificationCode,
            SupportMail: supportEmail,
            InfoFooter: infoFooter,
            DeaboUrl: deaboUrl,
            SettingsUrl: settingsUrl);

        return await VerificationHtmlComposer.BuildAsync(content, cancellationToken).ConfigureAwait(false);
    }
}
