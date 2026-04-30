using Hermes.Notifications.Sending.HtmlLayout;
using Hermes.Notifications.Sending.HtmlLayout.Models;
using System.Net;

namespace Hermes.Notifications.Sending;

/// <summary>Loads <c>Verification.html</c> and replaces template tokens with escaped values.</summary>
public class VerificationHtmlComposer
{
    /// <summary>
    /// Substitutes placeholders in <c>Verification.html</c> with UTF-8 HTML-safe values.
    /// </summary>
    public async Task<string> BuildAsync(
        VerificationContent verificationContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verificationContent);

        var assembly = typeof(VerificationHtmlComposer).Assembly;

        var verificationTpl = await FileReaderHelper.ReadEmbeddedTemplateAsync(assembly, "Verification.html", cancellationToken).ConfigureAwait(false);

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
