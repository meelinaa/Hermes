namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Defines the contract for scheduling background jobs related to newsletter processing.
/// </summary>
public interface INewsletterSchedulerJobService
{
    /// <summary>
    /// Requests an execution of the newsletter scheduler after a mutation to newsletter data has occurred.
    /// </summary>
    void RequestRunAfterNewsMutation();
}
