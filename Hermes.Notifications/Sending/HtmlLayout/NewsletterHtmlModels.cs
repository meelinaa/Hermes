namespace Hermes.Notifications.Sending.HtmlLayout;

/// <summary>
/// Placeholder values for the newsletter header block (<c>NewsletterHeader.html</c>).
/// </summary>
public sealed record NewsletterHeaderContent(
    string Header,
    string Header2,
    string DateDisplay,
    string Intro);

/// <summary>
/// One article row rendered with <c>NewsletterItem.html</c>.
/// </summary>
public sealed record NewsletterItemContent(
    string Category,
    string Title,
    string Content,
    string Url,
    string ImageUrl);

/// <summary>
/// Placeholder values for the newsletter footer block (<c>NewsletterFooter.html</c>).
/// </summary>
public sealed record NewsletterFooterContent(
    string InfoFooter,
    string DeaboUrl,
    string SettingsUrl);
