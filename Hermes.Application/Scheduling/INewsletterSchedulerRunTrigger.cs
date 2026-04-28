namespace Hermes.Application.Scheduling;

/// <summary>
/// Asks the Hangfire-backed newsletter scheduler to run (e.g. after news settings are created, updated, or removed).
/// </summary>
public interface INewsletterSchedulerRunTrigger
{
    /// <summary>
    /// Enqueues an immediate execution of the recurring scheduler job; failures are logged and do not fail the caller.
    /// </summary>
    void RequestRunAfterNewsMutation();
}
