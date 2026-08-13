using System.Globalization;
using Hermes.Application.DTOs.Email;
using Hermes.Application.DTOs.NewsArticle;
using Hermes.Application.Mapping;
using Hermes.Application.Options.External;
using Hermes.Application.Options.Newsletter;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Microsoft.Extensions.Logging;
using Hermes.Application.Logging;
using Microsoft.Extensions.Options;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Services.Newsletter;

/// <summary>
/// Service implementation for fetching external news articles, rendering HTML newsletter digests, sending email notifications, and recording delivery attempts.
/// </summary>
public sealed class NewsletterDigestService(
    IUserRepository users,
    INewsletterSubscriptionRepository newsletterSubscriptions,
    INotificationLogRepository notificationLogs,
    IArticleFetchingService articleFetchingService,
    IEmailProvider emailSender,
    INewsletterHtmlService newsletterRenderer,
    IOptions<NewsletterOptions> newsletterOptions,
    TimeProvider timeProvider,
    ILogger<NewsletterDigestService> logger) : INewsletterDigestService
{
    private const int MAX_ARTICLES_IN_NEWSLETTER = 5;
    private readonly CultureInfo _digestCulture = new("de-DE");

    /// <summary>
    /// Executes the end-to-end newsletter digest workflow: verifies deduplication, fetches relevant news articles, renders template HTML, dispatches email, records status, and advances schedule slot.
    /// </summary>
    /// <param name="userId">The unique identifier of the receiving user.</param>
    /// <param name="newsId">The unique identifier of the newsletter subscription.</param>
    /// <param name="digestSlotStartUtc">The UTC timestamp marking the start of the current digest schedule window.</param>
    /// <param name="cancellationToken">A token to observe while waiting for async operations to complete.</param>
    public async Task SendAsync(UserId userId, NewsletterId newsId, DateTime digestSlotStartUtc, CancellationToken cancellationToken = default)
    {
        if (userId.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User ID must be positive.");
        if (newsId.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(newsId), "News ID must be positive.");

        DateTime windowStart = DateTime.SpecifyKind(digestSlotStartUtc, DateTimeKind.Utc);
        windowStart = new DateTime(windowStart.Year, windowStart.Month, windowStart.Day, windowStart.Hour, windowStart.Minute, 0, DateTimeKind.Utc);
        DateTime windowEnd = windowStart.AddMinutes(1);

        bool duplicate = await notificationLogs
            .ExistsSentNotificationInWindowAsync(userId, newsId, windowStart, windowEnd, cancellationToken)
            .ConfigureAwait(false);
            
        if (duplicate)
        {
            await AdvanceNextDigestSlotAsync(newsId, userId, windowEnd, cancellationToken).ConfigureAwait(false);
            return;
        }

        User? user = await users.GetUserEntityByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || string.IsNullOrWhiteSpace(user.Email.Value))
            return;

        NewsletterSubscription? subscription = await newsletterSubscriptions.GetNewsByIdAsync(userId, newsId, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
            return;

        if (!subscription.IsEnabled)
        {
            await AdvanceNextDigestSlotAsync(newsId, userId, windowEnd, cancellationToken).ConfigureAwait(false);
            return;
        }

        IReadOnlyList<NewsArticle> articles = await articleFetchingService.FetchArticlesForSubscriptionAsync(subscription, cancellationToken).ConfigureAwait(false);
        if (articles.Count == 0)
        {
            await AdvanceNextDigestSlotAsync(newsId, userId, windowEnd, cancellationToken).ConfigureAwait(false);
            return;
        }

        string? subject = $"Hermes Newsletter (#{newsId.Value}) — {timeProvider.GetUtcNow().UtcDateTime.ToString("d", _digestCulture)}";

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
                    new EmailRecipientDto(user.Email!.Value.Trim(), string.IsNullOrWhiteSpace(user.Name) ? null : user.Name),
                    subject,
                    body),
                cancellationToken).ConfigureAwait(false);

            await notificationLogs.SetNotificationLogAsync(
                new NotificationLog
                {
                    UserId = userId,
                    NewsId = newsId,
                    SentAt = timeProvider.GetUtcNow().UtcDateTime,
                    Status = NotificationStatus.Sent,
                    Channel = DeliveryChannel.Email
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            logger.LogNewsletterDigestCanceled(userId.Value, newsId.Value);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogNewsletterDigestFailed(ex, userId.Value, newsId.Value);
            await notificationLogs.SetNotificationLogAsync(
                new NotificationLog
                {
                    UserId = userId,
                    NewsId = newsId,
                    SentAt = timeProvider.GetUtcNow().UtcDateTime,
                    Status = NotificationStatus.Failed,
                    Channel = DeliveryChannel.Email,
                    ErrorMessage = ex.Message
                },
                cancellationToken).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await AdvanceNextDigestSlotAsync(newsId, userId, windowEnd, cancellationToken).ConfigureAwait(false);
        }
    }

    private ValueTask AdvanceNextDigestSlotAsync(NewsletterId newsId, UserId userId, DateTime windowEnd, CancellationToken cancellationToken)
    {
        TimeZoneInfo zone = NewsletterSchedulingProvider.ResolveTimeZone(newsletterOptions.Value.TimeZoneId);
        return newsletterSubscriptions.AdvanceNextDigestSlotAsync(newsId, userId, zone, windowEnd, cancellationToken);
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
