using Hermes.Application.Services;

namespace Hermes.Worker.Jobs;

/// <summary>
/// Hangfire-invokable notification work: loads one news profile, fetches articles via NewsData.io, composes HTML with <see cref="NewsletterHtmlComposer"/>, sends e-mail, writes <see cref="NotificationLog"/> rows.
/// </summary>
public sealed class NotificationJobs(
    INewsletterDigestService newsletterDigestService,
    ILogger<NotificationJobs> logger)
{
    /// <summary>
    /// Sends one digest e-mail for <paramref name="newsId"/> (owned by <paramref name="userId"/>) if none was already sent for the same one-minute UTC slot.
    /// <paramref name="digestSlotStartUtc"/> is the UTC instant of the worker host’s scheduled local minute, from <see cref="Hermes.Worker.Scheduling.NewsletterScheduler"/>.
    /// </summary>
    public async Task SendNewsDigestAsync(int userId, int newsId, DateTime digestSlotStartUtc, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[NotificationJobs] Trigger digest for user {UserId}, news {NewsId}.", userId, newsId);
        await newsletterDigestService.SendAsync(userId, newsId, digestSlotStartUtc, cancellationToken).ConfigureAwait(false);
    }
}
