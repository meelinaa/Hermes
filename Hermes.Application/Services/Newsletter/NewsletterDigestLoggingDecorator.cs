using System;
using System.Threading;
using System.Threading.Tasks;
using FluentResults;
using Hermes.Application.Options.Newsletter;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hermes.Application.Services.Newsletter;

public sealed class NewsletterDigestLoggingDecorator : INewsletterDigestService
{
    private readonly INewsletterDigestService _inner;
    private readonly INotificationLogRepository _notificationLogs;
    private readonly INewsletterSubscriptionRepository _newsletterSubscriptions;
    private readonly IOptions<NewsletterOptions> _newsletterOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NewsletterDigestLoggingDecorator> _logger;

    public NewsletterDigestLoggingDecorator(
        INewsletterDigestService inner,
        INotificationLogRepository notificationLogs,
        INewsletterSubscriptionRepository newsletterSubscriptions,
        IOptions<NewsletterOptions> newsletterOptions,
        TimeProvider timeProvider,
        ILogger<NewsletterDigestLoggingDecorator> logger)
    {
        _inner = inner;
        _notificationLogs = notificationLogs;
        _newsletterSubscriptions = newsletterSubscriptions;
        _newsletterOptions = newsletterOptions;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<bool>> SendAsync(UserId userId, NewsletterId newsId, DateTime digestSlotStartUtc, CancellationToken cancellationToken = default)
    {
        DateTime windowStart = DateTime.SpecifyKind(digestSlotStartUtc, DateTimeKind.Utc);
        windowStart = new DateTime(windowStart.Year, windowStart.Month, windowStart.Day, windowStart.Hour, windowStart.Minute, 0, DateTimeKind.Utc);
        DateTime windowEnd = windowStart.AddMinutes(1);

        bool duplicate = await _notificationLogs
            .ExistsSentNotificationInWindowAsync(userId, newsId, windowStart, windowEnd, cancellationToken)
            .ConfigureAwait(false);

        if (duplicate)
        {
            await AdvanceNextDigestSlotAsync(newsId, userId, windowEnd, cancellationToken).ConfigureAwait(false);
            return Result.Ok(false);
        }

        try
        {
            var result = await _inner.SendAsync(userId, newsId, digestSlotStartUtc, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess && result.Value)
            {
                var logSuccess = NotificationLog.Create(userId, DeliveryChannel.Email, _timeProvider.GetUtcNow().UtcDateTime, newsId);
                logSuccess.MarkAsSent();

                await _notificationLogs.SetNotificationLogAsync(logSuccess, cancellationToken).ConfigureAwait(false);
            }
            else if (result.IsFailed)
            {
                _logger.LogError("Newsletter digest failed for user {UserId} and news {NewsId}: {Error}", userId.Value, newsId.Value, result.Errors.FirstOrDefault()?.Message);
                var logError = NotificationLog.Create(userId, DeliveryChannel.Email, _timeProvider.GetUtcNow().UtcDateTime, newsId);
                logError.MarkAsFailed(result.Errors.FirstOrDefault()?.Message ?? "Unknown error", null);
                
                await _notificationLogs.SetNotificationLogAsync(logError, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Newsletter digest operation was canceled for user {UserId} and news {NewsId}.", userId.Value, newsId.Value);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during newsletter digest for user {UserId} and news {NewsId}.", userId.Value, newsId.Value);
            var logError = NotificationLog.Create(userId, DeliveryChannel.Email, _timeProvider.GetUtcNow().UtcDateTime, newsId);
            logError.MarkAsFailed(ex.Message, null);
            await _notificationLogs.SetNotificationLogAsync(logError, cancellationToken).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await AdvanceNextDigestSlotAsync(newsId, userId, windowEnd, cancellationToken).ConfigureAwait(false);
        }
    }

    private ValueTask AdvanceNextDigestSlotAsync(NewsletterId newsId, UserId userId, DateTime windowEnd, CancellationToken cancellationToken)
    {
        TimeZoneInfo zone = NewsletterSchedulingProvider.ResolveTimeZone(_newsletterOptions.Value.TimeZoneId);
        return _newsletterSubscriptions.AdvanceNextDigestSlotAsync(newsId, userId, zone, windowEnd, cancellationToken);
    }
}
