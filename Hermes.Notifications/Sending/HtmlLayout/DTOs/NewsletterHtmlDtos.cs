namespace Hermes.Notifications.Sending.HtmlLayout.DTOs;

/// <summary>
/// Data transfer object for rendering newsletter HTML header content.
/// </summary>
public sealed record NewsletterHeaderContentDto(
    string Header,
    string Header2,
    string DateDisplay,
    string Intro);

/// <summary>
/// Data transfer object for rendering individual newsletter article item content.
/// </summary>
public sealed record NewsletterItemContentDto(
    string Category,
    string Title,
    string Content,
    string Url,
    string ImageUrl);

/// <summary>
/// Data transfer object for rendering newsletter HTML footer content.
/// </summary>
public sealed record NewsletterFooterContentDto(
    string InfoFooter,
    string DeaboUrl,
    string SettingsUrl);
