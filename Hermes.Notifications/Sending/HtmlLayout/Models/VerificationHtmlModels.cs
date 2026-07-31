namespace Hermes.Notifications.Sending.HtmlLayout.Models;

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
