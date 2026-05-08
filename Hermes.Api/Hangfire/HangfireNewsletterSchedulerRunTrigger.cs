using Hangfire;
using Hermes.Application.Scheduling;
using Microsoft.Extensions.Logging;

namespace Hermes.Api.Hangfire;

/// <summary>
/// Triggers the worker’s recurring newsletter scheduler via shared Hangfire MySQL storage.
/// </summary>
public sealed class HangfireNewsletterSchedulerRunTrigger(JobStorage jobStorage, ILogger<HangfireNewsletterSchedulerRunTrigger> logger)
    : INewsletterSchedulerRunTrigger
{
    public void RequestRunAfterNewsMutation()
    {
        try
        {
            new RecurringJobManager(jobStorage).TriggerJob(NewsletterSchedulerRecurringJob.ID);
            logger.LogInformation(
                "Triggered Hangfire recurring job {JobId} after news mutation.",
                NewsletterSchedulerRecurringJob.ID);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not trigger Hangfire recurring job {JobId} after news mutation; hourly schedule still applies.",
                NewsletterSchedulerRecurringJob.ID);
        }
    }
}
