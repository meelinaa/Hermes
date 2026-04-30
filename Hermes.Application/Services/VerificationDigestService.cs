using System.Globalization;
using System.Security.Cryptography;
using Hermes.Application.Models.Email;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Notifications.Sending;
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

    public async Task SendAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");

        var user = await dataStore.GetUserEntityByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
            return;

        var code = GenerateNumericVerificationCode();
        var expiresAt = DateTime.UtcNow.AddMinutes(VERIFICATION_CODE_VALIDITY_MINUTES);

        await dataStore
            .SetUserEmailVerificationChallengeAsync(userId, code, expiresAt, cancellationToken)
            .ConfigureAwait(false);

        var site = siteUrlsOptions.Value;
        var baseUrl = (site.PublicBaseUrl ?? "https://hermes.de").TrimEnd('/');
        var supportEmail = (site.SupportEmail ?? "support@hermes.de").Trim();
        var body = await BuildVerificationBodyAsync(
                user.Name,
                user.Email.Trim(),
                code,
                supportEmail,
                $"{baseUrl}/unsubscribe",
                $"{baseUrl}/settings",
                cancellationToken)
            .ConfigureAwait(false);

        var subject = $"Hermes — Konto-Verifizierung";

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

    private static string GenerateNumericVerificationCode()
    {
        var n = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return n.ToString("D6", CultureInfo.InvariantCulture);
    }

    private static async Task<string> BuildVerificationBodyAsync(
        string? userDisplayName,
        string recipientEmail,
        string verificationCode,
        string supportEmail,
        string deaboUrl,
        string settingsUrl,
        CancellationToken cancellationToken)
    {
        var dateDisplay = DateTime.UtcNow.ToString("dd. MMMM yyyy", _digestCulture);

        var intro = string.IsNullOrWhiteSpace(userDisplayName)
            ? "Hallo,"
            : $"Hallo {userDisplayName.Trim()},";

        const string INTRO_2 =
            "Vielen Dank für Ihre Registrierung bei Hermes. Um Ihr Konto zu verifizieren, verwenden Sie bitte den folgenden Verifizierungscode:";

        var infoFooter = $"Diese E-Mail wurde an {recipientEmail} gesendet";

        var content = new VerificationContent(
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
