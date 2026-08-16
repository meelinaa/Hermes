namespace Hermes.Application.Ports.Inbound;

using Hermes.Domain.ValueObjects;

public interface INewsletterScheduleService
{
    Task<IReadOnlyList<(NewsletterId NewsId, UserId UserId)>> GetDueItemsAsync(
        DateTime nowLocal,
        DateTime slotStartUtc,
        DateTime slotEndUtc,
        CancellationToken cancellationToken = default);
}
