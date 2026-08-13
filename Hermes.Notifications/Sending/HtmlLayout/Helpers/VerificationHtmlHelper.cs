using System.Net;
using System.Reflection;
using Hermes.Notifications.Sending.HtmlLayout.DTOs;
using Hermes.Notifications.Sending.HtmlLayout.Providers;

namespace Hermes.Notifications.Sending.HtmlLayout.Builders;

/// <summary>
/// Internal builder class for assembling account verification HTML emails.
/// </summary>
public class VerificationHtmlHelper
{
    /// <summary>
    /// Assembles verification HTML content by substituting DTO values into the embedded Verification.html template.
    /// </summary>
    /// <param name="verificationContent">The verification content DTO.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete rendered verification HTML string.</returns>
    public static async Task<string> BuildAsync(
        VerificationContentDto verificationContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verificationContent);

        Assembly assembly = typeof(VerificationHtmlHelper).Assembly;

        string? verificationTpl = await EmbeddedTemplateProvider.ReadEmbeddedTemplateAsync(assembly, "Verification.html", cancellationToken).ConfigureAwait(false);

        static string Enc(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

        return verificationTpl
            .Replace("{{HEADER}}", Enc(verificationContent.Header), StringComparison.Ordinal)
            .Replace("{{HEADER2}}", Enc(verificationContent.Header2), StringComparison.Ordinal)
            .Replace("{{DATE}}", Enc(verificationContent.DateDisplay), StringComparison.Ordinal)
            .Replace("{{INTRO}}", Enc(verificationContent.Intro), StringComparison.Ordinal)
            .Replace("{{INTRO2}}", Enc(verificationContent.Intro2), StringComparison.Ordinal)
            .Replace("{{VERIFICATION_CODE}}", Enc(verificationContent.VerificationCode), StringComparison.Ordinal)
            .Replace("{{SUPPORTMAIL}}", Enc(verificationContent.SupportMail), StringComparison.Ordinal)
            .Replace("{{INFOFOOTER}}", Enc(verificationContent.InfoFooter), StringComparison.Ordinal)
            .Replace("{{DEABOURLFOOTER}}", Enc(verificationContent.DeaboUrl), StringComparison.Ordinal)
            .Replace("{{SETTINGSFOOTER}}", Enc(verificationContent.SettingsUrl), StringComparison.Ordinal);
    }
}
