using Hangfire;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Scheduling;
using Microsoft.Extensions.Logging;

namespace Hermes.Api.Hangfire;

public sealed class NewsletterSchedulerJobService(JobStorage jobStorage, ILogger<NewsletterSchedulerJobService> logger)
    : INewsletterSchedulerJobService
{
    /// <summary>
    /// Triggers the recurring newsletter scheduler Hangfire job immediately after a newsletter subscription mutation.
    /// This ensures that the system reacts promptly to subscription changes instead of waiting for the next scheduled tick.
    /// </summary>
    public void RequestRunAfterNewsMutation()
    {
        new RecurringJobManager(jobStorage).TriggerJob(NewsletterSchedulerRecurringService.ID);
        logger.LogInformation(
            "Triggered Hangfire recurring job {JobId} after news mutation.",
            NewsletterSchedulerRecurringService.ID);
    }
}
