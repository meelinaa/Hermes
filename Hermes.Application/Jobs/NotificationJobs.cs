using Hermes.Application.Services;
using Microsoft.Extensions.Logging;

namespace Hermes.Application.Jobs;

public sealed class NotificationJobs(
    INewsletterDigestService newsletterDigestService,
    IVerificationDigestService verificationDigestService)
{
    public async Task SendNewsDigestAsync(int userId, int newsId, DateTime digestSlotStartUtc, CancellationToken cancellationToken = default)
        => await newsletterDigestService.SendAsync(userId, newsId, digestSlotStartUtc, cancellationToken).ConfigureAwait(false);

    public async Task SendVerificationMailAsync(int userId, CancellationToken cancellationToken = default)
        => await verificationDigestService.SendAsync(userId, cancellationToken).ConfigureAwait(false);
}
