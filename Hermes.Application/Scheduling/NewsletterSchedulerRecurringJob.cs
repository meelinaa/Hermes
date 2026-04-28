namespace Hermes.Application.Scheduling;

/// <summary>
/// Hangfire recurring job id for the worker’s newsletter scheduler (runs every minute; shared by API trigger and worker registration).
/// </summary>
public static class NewsletterSchedulerRecurringJob
{
    public const string Id = "newsletter-scheduler-hourly";
}
