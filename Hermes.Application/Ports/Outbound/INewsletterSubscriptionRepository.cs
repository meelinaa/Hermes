namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Composite repository interface aggregating subscription CRUD operations (<see cref="INewsletterSubscriptionStore"/>)
/// and worker scheduling evaluation (<see cref="INewsletterSchedulerStore"/>).
/// </summary>
public interface INewsletterSubscriptionRepository : INewsletterSubscriptionStore, INewsletterSchedulerStore
{
}
