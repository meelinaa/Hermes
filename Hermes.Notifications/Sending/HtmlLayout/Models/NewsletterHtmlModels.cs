namespace Hermes.Notifications.Sending.HtmlLayout.Models;

public sealed record NewsletterHeaderContent(
    string Header,
    string Header2,
    string DateDisplay,
    string Intro);

public sealed record NewsletterItemContent(
    string Category,
    string Title,
    string Content,
    string Url,
    string ImageUrl);

public sealed record NewsletterFooterContent(
    string InfoFooter,
    string DeaboUrl,
    string SettingsUrl);
