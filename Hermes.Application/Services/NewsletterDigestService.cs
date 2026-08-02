using System.Globalization;
using Hermes.Application.DTOs.Email;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.Mapping;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Scheduling;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Services;

/// <summary>
/// Orchestrates the automated newsletter digest delivery pipeline for background jobs.
/// Executes deduplication checks within minute windows, queries external news providers,
/// formats top article previews, delegates HTML template rendering to <see cref="INewsletterHtmlService"/>,
/// dispatches emails, and logs execution audit trails.
/// </summary>
public sealed class NewsletterDigestService(
    IUserRepository users,
    INewsletterSubscriptionRepository newsletterSubscriptions,
    INotificationLogRepository notificationLogs,
    INewsArticleProvider newsArticleProvider,
    IEmailProvider emailSender,
    INewsletterHtmlService newsletterRenderer,
    IOptions<NewsDataIoOptions> newsDataOptions,
    IOptions<NewsletterOptions> newsletterOptions,
    ILogger<NewsletterDigestService> logger) : INewsletterDigestService
{
    private const int MAX_ARTICLES_IN_NEWSLETTER = 10;
    private static readonly CultureInfo _digestCulture = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>
    /// Executes the full newsletter digest pipeline for a specific user and subscription slot.
    /// Performs UTC minute deduplication to prevent duplicate emails, fetches matching news articles,
    /// renders localized HTML templates, dispatches the email, and records audit logs.
    /// Advances the subscription's next digest slot upon completion or permanent failure to prevent stuck job queues.
    /// </summary>
    /// <param name="userId">The unique identifier of the recipient user.</param>
    /// <param name="newsId">The unique identifier of the active newsletter subscription profile.</param>
    /// <param name="digestSlotStartUtc">The UTC timestamp representing the start of the scheduled digest execution slot.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests during async operations.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="userId"/> or <paramref name="newsId"/> is less than or equal to zero.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the required NewsDataIo API key configuration is missing or blank.</exception>
    public async Task SendAsync(int userId, int newsId, DateTime digestSlotStartUtc, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");
        if (newsId <= 0)
            throw new ArgumentOutOfRangeException(nameof(newsId), "News ID must be positive.");
        string? apiKey = newsDataOptions.Value.Key?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Configure NewsDataIo:Key.");

        DateTime windowStart = DateTime.SpecifyKind(digestSlotStartUtc, DateTimeKind.Utc);
        windowStart = new DateTime(windowStart.Year, windowStart.Month, windowStart.Day, windowStart.Hour, windowStart.Minute, 0, DateTimeKind.Utc);
        DateTime windowEnd = windowStart.AddMinutes(1);

        bool advanceDigestSlot = false;
        try
        {
            bool duplicate = await notificationLogs
                .ExistsSentNotificationInWindowAsync(userId, newsId, windowStart, windowEnd, cancellationToken)
                .ConfigureAwait(false);
            if (duplicate)
            {
                advanceDigestSlot = true;
                return;
            }

            User? user = await users.GetUserEntityByIdAsync(userId, cancellationToken).ConfigureAwait(false);
            if (user is null || string.IsNullOrWhiteSpace(user.Email))
                return;

            NewsletterSubscription? subscription = await newsletterSubscriptions.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
            if (subscription is null)
                return;

            if (!subscription.IsEnabled)
            {
                advanceDigestSlot = true;
                return;
            }

            NewsArticleQueryDto? query = BuildArticleQuery(apiKey, subscription);
            if (query is null)
                return;

            IReadOnlyList<NewsArticle> articles = await newsArticleProvider.GetLatestAsync(query, cancellationToken).ConfigureAwait(false);
            if (articles.Count == 0)
            {
                advanceDigestSlot = true;
                return;
            }

            string? subject = $"Hermes Newsletter (#{newsId}) — {DateTime.UtcNow.ToString("d", _digestCulture)}";

            List<NewsletterArticleItemDto> articleItems = articles
                .Take(MAX_ARTICLES_IN_NEWSLETTER)
                .Select(a => new NewsletterArticleItemDto(
                    Category: a.Category?.FirstOrDefault() ?? "News",
                    Title: a.Title ?? string.Empty,
                    Content: TruncatePlainText(a.Description, 150),
                    Url: a.Link ?? "#",
                    ImageUrl: a.ImageUrl ?? string.Empty))
                .ToList();

            NewsletterRenderRequestDto renderRequest = new(user.Name, articleItems);
            string body = await newsletterRenderer
                .RenderNewsletterAsync(renderRequest, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await emailSender.SendAsync(
                    new EmailMessageDto(
                        new EmailRecipientDto(user.Email.Trim(), string.IsNullOrWhiteSpace(user.Name) ? null : user.Name),
                        subject,
                        body),
                    cancellationToken).ConfigureAwait(false);

                await notificationLogs.SetNotificationLogAsync(
                    new NotificationLog
                    {
                        UserId = userId,
                        NewsId = newsId,
                        SentAt = DateTime.UtcNow,
                        Status = NotificationStatus.Sent,
                        Channel = DeliveryChannel.Email
                    },
                    cancellationToken).ConfigureAwait(false);
                advanceDigestSlot = true;
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Newsletter digest sending for user {UserId}, news {NewsId} was canceled.", userId, newsId);
                advanceDigestSlot = true;
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send newsletter digest for user {UserId}, news {NewsId}.", userId, newsId);
                await notificationLogs.SetNotificationLogAsync(
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
                advanceDigestSlot = true;
                throw;
            }
        }
        finally
        {
            if (advanceDigestSlot)
            {
                TimeZoneInfo zone = NewsletterSchedulingProvider.ResolveTimeZone(
                    newsletterOptions.Value.TimeZoneId);
                await newsletterSubscriptions
                    .AdvanceNextDigestSlotAsync(newsId, userId, zone, windowEnd, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Builds the external article query parameters from subscription filter criteria.
    /// Maps country names to ISO 3166-1 Alpha-2 codes, languages to ISO 639 codes, and combines keyword terms with OR operators.
    /// </summary>
    /// <param name="apiKey">The external API provider key.</param>
    /// <param name="subscription">The newsletter subscription containing user-selected topic filters.</param>
    /// <returns>A populated <see cref="NewsArticleQueryDto"/> if valid filters exist; otherwise <c>null</c> when no filter criteria are specified.</returns>
    private static NewsArticleQueryDto? BuildArticleQuery(string apiKey, NewsletterSubscription subscription)
    {
        List<string>? countries = subscription.Countries is { Count: > 0 }
            ? subscription.Countries.Select(CountryIsoCodeMapper.ToIso3166Alpha2).ToList()
            : null;
        List<string>? languages = subscription.Languages is { Count: > 0 }
            ? subscription.Languages.Select(LanguageIsoCodeMapper.ToIso639Code).ToList()
            : null;
        List<string>? categories = subscription.Category is { Count: > 0 }
            ? subscription.Category.Select(category => category.ToString().ToLowerInvariant()).ToList()
            : null;

        string? keywordsQuery = null;
        if (subscription.Keywords is { Count: > 0 })
        {
            List<string> terms = subscription.Keywords.Where(keyword => !string.IsNullOrWhiteSpace(keyword)).Select(keyword => keyword.Trim()).ToList();
            if (terms.Count > 0)
                keywordsQuery = string.Join(" OR ", terms);
        }

        if (countries is null && languages is null && categories is null && string.IsNullOrWhiteSpace(keywordsQuery))
            return null;

        return new NewsArticleQueryDto
        {
            ApiKey = apiKey,
            Countries = countries,
            Languages = languages,
            Categories = categories,
            KeywordsQuery = keywordsQuery
        };
    }

    /// <summary>
    /// Truncates raw text content to a specified maximum character length for digest card preview snippets.
    /// </summary>
    /// <param name="value">The raw input string to truncate.</param>
    /// <param name="maxLength">The maximum allowed character length including the suffix.</param>
    /// <param name="suffix">The string appended to indicate truncation (defaults to "...").</param>
    /// <returns>The truncated text snippet or empty string if input is null/empty.</returns>
    private static string TruncatePlainText(string? value, int maxLength, string suffix = "...")
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        if (value.Length <= maxLength)
            return value;
        return string.Concat(value.AsSpan(0, maxLength - suffix.Length), suffix);
    }
}
