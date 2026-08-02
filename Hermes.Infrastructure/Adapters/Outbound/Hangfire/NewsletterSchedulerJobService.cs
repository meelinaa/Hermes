using Hangfire;
using Microsoft.Extensions.Logging;

using Hermes.Application.Constants;
using Hermes.Application.Ports.Outbound;

namespace Hermes.Infrastructure.Adapters.Outbound.Hangfire;

/// <summary>
/// Infrastructure adapter implementation for triggering recurring newsletter scheduling runs in Hangfire.
/// </summary>
public sealed class NewsletterSchedulerJobService(JobStorage jobStorage, ILogger<NewsletterSchedulerJobService> logger)
    : INewsletterSchedulerJobService
{
    /// <summary>
    /// Triggers the recurring newsletter scheduler Hangfire job immediately after a newsletter subscription mutation.
    /// This ensures that the system reacts promptly to subscription changes instead of waiting for the next scheduled tick.
    /// </summary>
    public void RequestRunAfterNewsMutation()
    {
        new RecurringJobManager(jobStorage).TriggerJob(RecurringJobConstants.ID);
        logger.LogInformation(
            "Newsletter scheduler execution requested manually for job {JobId}",
            RecurringJobConstants.ID);
    }
}
