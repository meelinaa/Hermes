using Hangfire;
using Hermes.Application.Scheduling;
using Microsoft.Extensions.Logging;

namespace Hermes.Api.Hangfire;

public sealed class NewsletterSchedulerJobService(JobStorage jobStorage, ILogger<NewsletterSchedulerJobService> logger)
    : INewsletterSchedulerJobService
{
    public void RequestRunAfterNewsMutation()
    {
        try
        {
            new RecurringJobManager(jobStorage).TriggerJob(NewsletterSchedulerRecurringService.ID);
            logger.LogInformation(
                "Triggered Hangfire recurring job {JobId} after news mutation.",
                NewsletterSchedulerRecurringService.ID);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not trigger Hangfire recurring job {JobId} after news mutation; hourly schedule still applies.",
                NewsletterSchedulerRecurringService.ID);
        }
    }
}
