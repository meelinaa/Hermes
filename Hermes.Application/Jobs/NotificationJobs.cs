using Hermes.Application.Services;
using Microsoft.Extensions.Logging;

namespace Hermes.Application.Jobs;

/// <summary>
/// Hangfire-invokable notification work: newsletter digests and verification e-mail.
/// </summary>
public sealed class NotificationJobs(
    INewsletterDigestService newsletterDigestService,
    IVerificationDigestService verificationDigestService)
{
    /// <summary>
    /// Sends one digest e-mail for <paramref name="newsId"/> (owned by <paramref name="userId"/>) if none was already sent for the same one-minute UTC slot.
    /// <paramref name="digestSlotStartUtc"/> is the UTC instant of the worker host’s scheduled local minute, from <see cref="Hermes.Worker.Scheduling.NewsletterScheduler"/>.
    /// </summary>
    public async Task SendNewsDigestAsync(int userId, int newsId, DateTime digestSlotStartUtc, CancellationToken cancellationToken = default)
        => await newsletterDigestService.SendAsync(userId, newsId, digestSlotStartUtc, cancellationToken).ConfigureAwait(false);

    /// <summary>Sends verification e-mail with a fresh time-bound code (see <see cref="VerificationDigestService"/>).</summary>
    public async Task SendVerificationMailAsync(int userId, CancellationToken cancellationToken = default)
        => await verificationDigestService.SendAsync(userId, cancellationToken).ConfigureAwait(false);
}
