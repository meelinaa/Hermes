using System.Globalization;
using Hermes.Application.Mapping;
using Hermes.Application.Models.Email;
using Hermes.Application.Models.News;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Notifications.Sending.HtmlLayout;
using Hermes.Notifications.Sending.HtmlLayout.Models;
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
    private const int MAX_ARTICLES_IN_NEWSLETTER = 10;
    private static readonly CultureInfo _digestCulture = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Sends one newsletter digest for a due news configuration and records the delivery result.</summary>
    public async Task SendAsync(int userId, int newsId, DateTime digestSlotStartUtc, CancellationToken cancellationToken = default)
    {
        if(userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");
        if(newsId <= 0)
            throw new ArgumentOutOfRangeException(nameof(newsId), "News ID must be positive.");
        string? apiKey = newsDataOptions.Value.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Configure NewsDataIo:ApiKey.");

        DateTime windowStart = DateTime.SpecifyKind(digestSlotStartUtc, DateTimeKind.Utc);
        windowStart = new DateTime(windowStart.Year, windowStart.Month, windowStart.Day, windowStart.Hour, windowStart.Minute, 0, DateTimeKind.Utc);
        DateTime windowEnd = windowStart.AddMinutes(1);

        bool duplicate = await dataStore
            .ExistsSentNotificationInWindowAsync(userId, newsId, windowStart, windowEnd, cancellationToken)
            .ConfigureAwait(false);
        if (duplicate)
            return;

        User? user = await dataStore.GetUserEntityByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
            return;

        News? news = await dataStore.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
        if (news is null)
            return;

        NewsArticleQuery? query = BuildArticleQuery(apiKey, news);
        if (query is null)
            return;

        IReadOnlyList<NewsArticle> articles = await newsArticleProvider.GetLatestAsync(query, cancellationToken).ConfigureAwait(false);
        string? subject = $"Hermes Newsletter (#{newsId}) — {DateTime.UtcNow.ToString("d", _digestCulture)}";
        string? body = await BuildNewsletterBodyAsync(user.Name, articles, cancellationToken).ConfigureAwait(false);

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

    /// <summary>Builds a provider query from persisted news filters; returns <c>null</c> when no usable filter exists.</summary>
    private static NewsArticleQuery? BuildArticleQuery(string apiKey, News news)
    {
        List<string>? countries = news.Countries is { Count: > 0 }
            ? news.Countries.Select(CountryIsoCodeMapper.ToIso3166Alpha2).ToList()
            : null;
        List<string>? languages = news.Languages is { Count: > 0 }
            ? news.Languages.Select(LanguageIsoCodeMapper.ToIso639Code).ToList()
            : null;
        List<string>? categories = news.Category is { Count: > 0 }
            ? news.Category.Select(category => category.ToString().ToLowerInvariant()).ToList()
            : null;

        string? keywordsQuery = null;
        if (news.Keywords is { Count: > 0 })
        {
            List<string> terms = news.Keywords.Where(keyword => !string.IsNullOrWhiteSpace(keyword)).Select(keyword => keyword.Trim()).ToList();
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

    /// <summary>Builds the newsletter HTML body for the selected article set.</summary>
    private static async Task<string> BuildNewsletterBodyAsync(
        string? userDisplayName,
        IReadOnlyList<NewsArticle> articles,
        CancellationToken cancellationToken)
    {
        const int MAX_TEXT_LENGTH = 150;
        NewsletterHtmlComposer composer = new();
        string? dateDisplay = DateTime.UtcNow.ToString("dddd, dd. MMMM yyyy", _digestCulture);

        string? greetings = DateTime.UtcNow.Hour switch
        {
            < 12 => "Guten Morgen",
            < 18 => "Guten Tag",
            _ => "Guten Abend"
        };

        string? intro = string.IsNullOrWhiteSpace(userDisplayName)
            ? $"{greetings}! Hier sind die wichtigsten Nachrichten."
            : $"{greetings}, {userDisplayName}! Hier sind die wichtigsten Nachrichten.";

        NewsletterHeaderContent header = new(
            Header: "HERMES",
            Header2: "Dein täglicher News-Überblick",
            DateDisplay: dateDisplay,
            Intro: intro);

        List<NewsletterItemContent> itemModels = articles
            .Take(MAX_ARTICLES_IN_NEWSLETTER)
            .Select(article => new NewsletterItemContent(
                Category: article.Category?.FirstOrDefault() ?? "News",
                Title: article.Title ?? string.Empty,
                Content: TruncatePlainText(article.Description, MAX_TEXT_LENGTH),
                Url: article.Link ?? "#",
                ImageUrl: article.ImageUrl ?? string.Empty))
            .ToList();

        NewsletterFooterContent footer = new(
            InfoFooter: "Du erhältst diese E-Mail, weil du den Hermes Newsletter abonniert hast.",
            DeaboUrl: "#",
            SettingsUrl: "#");

        return await NewsletterHtmlComposer.BuildAsync(header, itemModels, footer, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Truncates plain text to a maximum length and appends a suffix when shortened.</summary>
    private static string TruncatePlainText(string? value, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        if (value.Length <= maxLength)
            return value;
        return string.Concat(value.AsSpan(0, maxLength - suffix.Length), suffix);
    }
}
