namespace Hermes.Notifications.Sending.HtmlLayout.DTOs;

/// <summary>
/// Data transfer object for rendering verification email HTML content.
/// </summary>
public sealed record VerificationContentDto(
    string Header,
    string Header2,
    string DateDisplay,
    string Intro,
    string Intro2,
    string VerificationCode,
    string SupportMail,
    string InfoFooter,
    string DeaboUrl,
    string SettingsUrl);
