namespace Hermes.Application.Services;

public interface INewsletterScheduleService
{
    Task<IReadOnlyList<(int NewsId, int UserId)>> GetDueItemsAsync(
        DateTime nowLocal,
        DateTime slotStartUtc,
        DateTime slotEndUtc,
        CancellationToken cancellationToken = default);
}
