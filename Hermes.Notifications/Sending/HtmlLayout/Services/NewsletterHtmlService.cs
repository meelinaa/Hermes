using System.Globalization;
using Hermes.Application.DTOs.Email;
using Hermes.Application.Ports.Outbound;
using Hermes.Notifications.Sending.HtmlLayout.Builders;
using Hermes.Notifications.Sending.HtmlLayout.DTOs;

namespace Hermes.Notifications.Sending.HtmlLayout.Services;

/// <summary>
/// Produces newsletter HTML by mapping Application-layer render requests
/// to the internal <see cref="NewsletterHtmlBuilder"/> templates.
/// Keeps HTML templating concerns inside the Notifications boundary.
/// </summary>
public sealed class NewsletterHtmlService : INewsletterHtmlService
{
    private const int MAX_ARTICLES = 10;
    private static readonly CultureInfo _culture = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>
    /// Renders a complete newsletter HTML body from the supplied request data
    /// by delegating to <see cref="NewsletterHtmlBuilder"/>.
    /// </summary>
    /// <param name="request">The newsletter render request DTO.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered newsletter HTML string.</returns>
    public async Task<string> RenderNewsletterAsync(
        NewsletterRenderRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string dateDisplay = DateTime.UtcNow.ToString("dddd, dd. MMMM yyyy", _culture);

        string greeting = DateTime.UtcNow.Hour switch
        {
            < 12 => "Guten Morgen",
            < 18 => "Guten Tag",
            _ => "Guten Abend"
        };

        string intro = string.IsNullOrWhiteSpace(request.UserDisplayName)
            ? $"{greeting}! Hier sind die wichtigsten Nachrichten."
            : $"{greeting}, {request.UserDisplayName}! Hier sind die wichtigsten Nachrichten.";

        NewsletterHeaderContentDto header = new(
            Header: "HERMES",
            Header2: "Dein täglicher News-Überblick",
            DateDisplay: dateDisplay,
            Intro: intro);

        List<NewsletterItemContentDto> items = request.Articles
            .Take(MAX_ARTICLES)
            .Select(a => new NewsletterItemContentDto(
                Category: a.Category,
                Title: a.Title,
                Content: a.Content,
                Url: a.Url,
                ImageUrl: a.ImageUrl))
            .ToList();

        NewsletterFooterContentDto footer = new(
            InfoFooter: "Du erhältst diese E-Mail, weil du den Hermes Newsletter abonniert hast.",
            DeaboUrl: "#",
            SettingsUrl: "#");

        return await NewsletterHtmlBuilder
            .BuildAsync(header, items, footer, cancellationToken)
            .ConfigureAwait(false);
    }
}
