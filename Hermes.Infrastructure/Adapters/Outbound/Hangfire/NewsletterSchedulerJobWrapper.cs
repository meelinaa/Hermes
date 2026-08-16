using Hangfire;
using Microsoft.Extensions.Logging;

using Hermes.Application.Constants;
using Hermes.Application.Ports.Outbound;
using Hermes.Infrastructure.Logging;

namespace Hermes.Infrastructure.Adapters.Outbound.Hangfire;

/// <summary>
/// Infrastructure adapter implementation for triggering recurring newsletter scheduling runs in Hangfire.
/// </summary>
public sealed class NewsletterSchedulerJobWrapper(JobStorage jobStorage, ILogger<NewsletterSchedulerJobWrapper> logger)
    : INewsletterSchedulerJobService
{
    /// <inheritdoc />
    public void RequestRunAfterNewsMutation()
    {
        new RecurringJobManager(jobStorage).TriggerJob(RecurringJobConstants.ID);
        logger.LogManualSchedulerExecutionRequested(RecurringJobConstants.ID);
    }
}
