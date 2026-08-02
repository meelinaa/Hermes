using Hermes.Application.Ports.Inbound;

namespace Hermes.Application.Services.NotificationLogs;

/// <summary>
/// Application job service responsible for delegating background notification execution (digest sending & verification mail sending).
/// </summary>
public sealed class NotificationJobService(
    INewsletterDigestService newsletterDigestService,
    IVerificationDigestService verificationDigestService)
{
    /// <summary>
    /// Executes the sending of a news digest asynchronously for the specified user and news article.
    /// </summary>
    /// <param name="userId">The ID of the target user.</param>
    /// <param name="newsId">The ID of the target news article.</param>
    /// <param name="digestSlotStartUtc">The UTC timestamp of the digest slot.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendNewsDigestAsync(int userId, int newsId, DateTime digestSlotStartUtc, CancellationToken cancellationToken = default)
        => await newsletterDigestService.SendAsync(userId, newsId, digestSlotStartUtc, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Executes the sending of a user verification email asynchronously.
    /// </summary>
    /// <param name="userId">The ID of the user to verify.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendVerificationMailAsync(int userId, CancellationToken cancellationToken = default)
        => await verificationDigestService.SendAsync(userId, cancellationToken).ConfigureAwait(false);
}
