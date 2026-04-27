using System.Globalization;
using Hermes.Application.Models.Email;
using Hermes.Application.Models.News;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.Mapping;
using Hermes.Notifications.Sending.HtmlLayout;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Services;

public sealed class NewsletterDigestService(
    IHermesDataStore dataStore,
    INewsArticleProvider newsArticleProvider,
    IEmailSender emailSender,
    IOptions<NewsDataIoOptions> newsDataOptions,
    ILogger<NewsletterDigestService> logger) : INewsletterDigestService
{
    private const int MaxArticlesInNewsletter = 10;
    private static readonly CultureInfo DigestCulture = CultureInfo.GetCultureInfo("de-DE");

    public async Task SendAsync(int userId, int newsId, DateTime digestSlotStartUtc, CancellationToken cancellationToken = default)
    {
        var apiKey = newsDataOptions.Value.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Configure NewsDataIo:ApiKey.");

        var windowStart = DateTime.SpecifyKind(digestSlotStartUtc, DateTimeKind.Utc);
        windowStart = new DateTime(windowStart.Year, windowStart.Month, windowStart.Day, windowStart.Hour, windowStart.Minute, 0, DateTimeKind.Utc);
        var windowEnd = windowStart.AddMinutes(1);

        var duplicate = await dataStore
            .ExistsSentNotificationInWindowAsync(userId, newsId, windowStart, windowEnd, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
            return;

        var user = await dataStore.GetUserEntityByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
            return;

        var news = await dataStore.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
        if (news is null)
            return;

        var query = BuildArticleQuery(apiKey, news);
        if (query is null)
            return;

        var articles = await newsArticleProvider.GetLatestAsync(query, cancellationToken).ConfigureAwait(false);
        var subject = $"Hermes Newsletter (#{newsId}) — {DateTime.UtcNow.ToString("d", DigestCulture)}";
        var body = await BuildNewsletterBodyAsync(user.Name, articles, cancellationToken).ConfigureAwait(false);

        try
        {
            await emailSender.SendAsync(
                new EmailMessage(
                    new EmailRecipient(user.Email.Trim(), string.IsNullOrWhiteSpace(user.Name) ? null : user.Name),
                    subject,
                    body),
                cancellationToken).ConfigureAwait(false);

            await dataStore.SetNotificationLogAsync(
                new NotificationLog
                {
                    UserId = userId,
                    NewsId = newsId,
                    SentAt = DateTime.UtcNow,
                    Status = NotificationStatus.Sent,
                    Channel = DeliveryChannel.Email
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send newsletter digest for user {UserId}, news {NewsId}.", userId, newsId);
            await dataStore.SetNotificationLogAsync(
                new NotificationLog
                {
                    UserId = userId,
                    NewsId = newsId,
                    SentAt = DateTime.UtcNow,
                    Status = NotificationStatus.Failed,
                    Channel = DeliveryChannel.Email,
                    ErrorMessage = ex.Message
                },
                cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static NewsArticleQuery? BuildArticleQuery(string apiKey, News news)
    {
        var countries = news.Countries is { Count: > 0 }
            ? news.Countries.Select(CountryIsoCodeMapper.ToIso3166Alpha2).ToList()
            : null;
        var languages = news.Languages is { Count: > 0 }
            ? news.Languages.Select(LanguageIsoCodeMapper.ToIso639Code).ToList()
            : null;
        var categories = news.Category is { Count: > 0 }
            ? news.Category.Select(c => c.ToString().ToLowerInvariant()).ToList()
            : null;

        string? keywordsQuery = null;
        if (news.Keywords is { Count: > 0 })
        {
            var terms = news.Keywords.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).ToList();
            if (terms.Count > 0)
                keywordsQuery = string.Join(" OR ", terms);
        }

        if (countries is null && languages is null && categories is null && string.IsNullOrWhiteSpace(keywordsQuery))
            return null;

        return new NewsArticleQuery
        {
            ApiKey = apiKey,
            Countries = countries,
            Languages = languages,
            Categories = categories,
            KeywordsQuery = keywordsQuery
        };
    }

    private static async Task<string> BuildNewsletterBodyAsync(
        string? userDisplayName,
        IReadOnlyList<NewsArticle> articles,
        CancellationToken cancellationToken)
    {
        const int maxTextLength = 150;
        var composer = new NewsletterHtmlComposer();
        var dateDisplay = DateTime.UtcNow.ToString("dddd, dd. MMMM yyyy", DigestCulture);

        var greetings = DateTime.UtcNow.Hour switch
        {
            < 12 => "Guten Morgen",
            < 18 => "Guten Tag",
            _ => "Guten Abend"
        };

        var intro = string.IsNullOrWhiteSpace(userDisplayName)
            ? $"{greetings}! Hier sind die wichtigsten Nachrichten."
            : $"{greetings}, {userDisplayName}! Hier sind die wichtigsten Nachrichten.";

        var header = new NewsletterHeaderContent(
            Header: "HERMES",
            Header2: "Dein täglicher News-Überblick",
            DateDisplay: dateDisplay,
            Intro: intro);

        var itemModels = articles
            .Take(MaxArticlesInNewsletter)
            .Select(a => new NewsletterItemContent(
                Category: a.Category?.FirstOrDefault() ?? "News",
                Title: a.Title ?? string.Empty,
                Content: TruncatePlainText(a.Description, maxTextLength),
                Url: a.Link ?? "#",
                ImageUrl: a.ImageUrl ?? string.Empty))
            .ToList();

        var footer = new NewsletterFooterContent(
            InfoFooter: "Du erhältst diese E-Mail, weil du den Hermes Newsletter abonniert hast.",
            DeaboUrl: "#",
            SettingsUrl: "#");

        return await composer.BuildAsync(header, itemModels, footer, cancellationToken).ConfigureAwait(false);
    }

    private static string TruncatePlainText(string? value, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        if (value.Length <= maxLength)
            return value;
        return string.Concat(value.AsSpan(0, maxLength - suffix.Length), suffix);
    }
}
