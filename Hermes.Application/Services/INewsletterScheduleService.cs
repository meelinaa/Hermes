namespace Hermes.Application.Services;

public interface INewsletterScheduleService
{
    /// <param name="nowLocal">Wall-clock “now” in the newsletter time zone (weekday + clock for JSON fallback).</param>
    /// <param name="slotStartUtc">Inclusive UTC start of the one-minute dispatch window.</param>
    /// <param name="slotEndUtc">Exclusive UTC end of that window.</param>
    Task<IReadOnlyList<(int NewsId, int UserId)>> GetDueItemsAsync(
        DateTime nowLocal,
        DateTime slotStartUtc,
        DateTime slotEndUtc,
        CancellationToken cancellationToken = default);
}
