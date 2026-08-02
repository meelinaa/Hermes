namespace Hermes.Application.DTOs.Email;

/// <summary>
/// Data needed by the renderer to produce a complete verification HTML body.
/// Defined in Application so the interface stays infrastructure-agnostic.
/// </summary>
/// <param name="UserDisplayName">Optional display name for the recipient.</param>
/// <param name="RecipientEmail">The recipient's e-mail address.</param>
/// <param name="VerificationCode">The verification code to embed in the e-mail.</param>
/// <param name="SupportEmail">The support e-mail address to display in the e-mail footer.</param>
/// <param name="UnsubscribeUrl">The URL for unsubscribing from future communications.</param>
/// <param name="SettingsUrl">The URL for managing account/notification settings.</param>
public sealed record VerificationRenderRequest(
    string? UserDisplayName,
    string RecipientEmail,
    string VerificationCode,
    string SupportEmail,
    string UnsubscribeUrl,
    string SettingsUrl);
