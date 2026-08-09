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
    /// <inheritdoc />
    public void RequestRunAfterNewsMutation()
    {
        new RecurringJobManager(jobStorage).TriggerJob(RecurringJobConstants.ID);
        logger.LogInformation(
            "Newsletter scheduler execution requested manually for job {JobId}",
            RecurringJobConstants.ID);
    }
}
