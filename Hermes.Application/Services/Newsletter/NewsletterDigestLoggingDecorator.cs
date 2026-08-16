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

/// <summary>
/// Logging and idempotency decorator for newsletter digest dispatches.
/// Enforces two-phase atomic slot reservation, active lease protection, and audit persistence.
/// </summary>
public sealed class NewsletterDigestLoggingDecorator : INewsletterDigestService
{
    private static readonly TimeSpan ActiveLeaseGracePeriod = TimeSpan.FromSeconds(60);

    private readonly INewsletterDigestService _inner;
    private readonly INotificationLogRepository _notificationLogs;
    private readonly INewsletterSubscriptionRepository _newsletterSubscriptions;
    private readonly IOptions<NewsletterOptions> _newsletterOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NewsletterDigestLoggingDecorator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NewsletterDigestLoggingDecorator"/> class.
    /// </summary>
    /// <param name="inner">The inner newsletter digest service implementation.</param>
    /// <param name="notificationLogs">The notification audit log repository.</param>
    /// <param name="newsletterSubscriptions">The newsletter subscription repository.</param>
    /// <param name="newsletterOptions">Configured newsletter scheduling options.</param>
    /// <param name="timeProvider">The system time provider.</param>
    /// <param name="logger">The structured logger.</param>
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

    /// <summary>
    /// Executes the two-phase digest dispatch: atomically reserves the target slot, transmits the email via inner service, and finalizes audit log status.
    /// </summary>
    /// <param name="userId">The recipient user ID.</param>
    /// <param name="newsId">The newsletter subscription ID.</param>
    /// <param name="digestSlotStartUtc">The UTC schedule slot timestamp.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A Result indicating true if sent, or false if skipped.</returns>
    public async Task<Result<bool>> SendAsync(UserId userId, NewsletterId newsId, DateTime digestSlotStartUtc, CancellationToken cancellationToken = default)
    {
        DateTime windowStart = DateTime.SpecifyKind(digestSlotStartUtc, DateTimeKind.Utc);
        windowStart = new DateTime(windowStart.Year, windowStart.Month, windowStart.Day, windowStart.Hour, windowStart.Minute, 0, DateTimeKind.Utc);
        DateTime windowEnd = windowStart.AddMinutes(1);

        DateTime nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var proposedLog = NotificationLog.Create(userId, DeliveryChannel.Email, windowStart, newsId, nowUtc);

        SlotReservationResult reservation = await _notificationLogs
            .TryReserveSlotAsync(proposedLog, ActiveLeaseGracePeriod, cancellationToken)
            .ConfigureAwait(false);

        if (!reservation.IsAcquired || reservation.Log is null)
        {
            _logger.LogInformation(
                "Slot {Slot} for user {UserId} and news {NewsId} is not acquired (Status: {Status}). Skipping duplicate/in-flight dispatch.",
                windowStart,
                userId.Value,
                newsId.Value,
                reservation.Status);

            await AdvanceNextDigestSlotAsync(newsId, userId, windowEnd, cancellationToken).ConfigureAwait(false);
            return Result.Ok(false);
        }

        NotificationLog activeLog = reservation.Log;

        try
        {
            var result = await _inner.SendAsync(userId, newsId, digestSlotStartUtc, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess && result.Value)
            {
                activeLog.MarkAsSent(_timeProvider.GetUtcNow().UtcDateTime);
                await _notificationLogs.UpdateNotificationLogAsync(activeLog, cancellationToken).ConfigureAwait(false);
            }
            else if (result.IsFailed)
            {
                string error = result.Errors.FirstOrDefault()?.Message ?? "Unknown error";
                _logger.LogError("Newsletter digest failed for user {UserId} and news {NewsId}: {Error}", userId.Value, newsId.Value, error);
                activeLog.MarkAsFailed(error, null);
                await _notificationLogs.UpdateNotificationLogAsync(activeLog, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Newsletter digest operation was canceled for user {UserId} and news {NewsId}.", userId.Value, newsId.Value);
            activeLog.MarkAsFailed("Operation canceled", null);
            await _notificationLogs.UpdateNotificationLogAsync(activeLog, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during newsletter digest for user {UserId} and news {NewsId}.", userId.Value, newsId.Value);
            activeLog.MarkAsFailed(ex.Message, null);
            await _notificationLogs.UpdateNotificationLogAsync(activeLog, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await AdvanceNextDigestSlotAsync(newsId, userId, windowEnd, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Advances the next schedule execution slot timestamp for the target subscription.
    /// </summary>
    /// <param name="newsId">The newsletter subscription ID.</param>
    /// <param name="userId">The recipient user ID.</param>
    /// <param name="windowEnd">The reference window end timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ValueTask representing the async operation.</returns>
    private ValueTask AdvanceNextDigestSlotAsync(NewsletterId newsId, UserId userId, DateTime windowEnd, CancellationToken cancellationToken)
    {
        TimeZoneInfo zone = NewsletterSchedulingProvider.ResolveTimeZone(_newsletterOptions.Value.TimeZoneId);
        return _newsletterSubscriptions.AdvanceNextDigestSlotAsync(newsId, userId, zone, windowEnd, cancellationToken);
    }
}
