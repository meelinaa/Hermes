using System.Globalization;
using Hermes.Application.DTOs.Email;
using Hermes.Application.Ports.Outbound;
using Hermes.Notifications.Sending.HtmlLayout.Builders;
using Hermes.Notifications.Sending.HtmlLayout.DTOs;

namespace Hermes.Notifications.Sending.HtmlLayout.Services;

/// <summary>
/// Produces verification HTML by mapping Application-layer render requests
/// to the internal <see cref="VerificationHtmlBuilder"/> templates.
/// Keeps HTML templating concerns inside the Notifications boundary.
/// </summary>
public sealed class VerificationHtmlService : IVerificationHtmlService
{
    private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>
    /// Renders a complete verification HTML body from the supplied request data
    /// by delegating to <see cref="VerificationHtmlBuilder"/>.
    /// </summary>
    /// <param name="request">The verification render request DTO.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered verification HTML string.</returns>
    public async Task<string> RenderVerificationAsync(
        VerificationRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string dateDisplay = DateTime.UtcNow.ToString("dd. MMMM yyyy", _culture);

        string intro = string.IsNullOrWhiteSpace(request.UserDisplayName)
            ? "Hallo,"
            : $"Hallo {request.UserDisplayName.Trim()},";

        const string INTRO2 =
            "Vielen Dank für Ihre Registrierung bei Hermes. Um Ihr Konto zu verifizieren, verwenden Sie bitte den folgenden Verifizierungscode:";

        string infoFooter = $"Diese E-Mail wurde an {request.RecipientEmail} gesendet";

        VerificationContentDto content = new(
            Header: "Hermes",
            Header2: "Konto-Verifizierung",
            DateDisplay: dateDisplay,
            Intro: intro,
            Intro2: INTRO2,
            VerificationCode: request.VerificationCode,
            SupportMail: request.SupportEmail,
            InfoFooter: infoFooter,
            DeaboUrl: request.UnsubscribeUrl,
            SettingsUrl: request.SettingsUrl);

        return await VerificationHtmlBuilder
            .BuildAsync(content, cancellationToken)
            .ConfigureAwait(false);
    }
}
