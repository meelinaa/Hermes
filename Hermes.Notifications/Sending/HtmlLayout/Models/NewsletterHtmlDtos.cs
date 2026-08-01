namespace Hermes.Notifications.Sending.HtmlLayout.Models;

public sealed record NewsletterHeaderContentDto(
    string Header,
    string Header2,
    string DateDisplay,
    string Intro);

public sealed record NewsletterItemContentDto(
    string Category,
    string Title,
    string Content,
    string Url,
    string ImageUrl);

public sealed record NewsletterFooterContentDto(
    string InfoFooter,
    string DeaboUrl,
    string SettingsUrl);
