namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// CQRS Read-Side interface for high-performance newsletter subscription queries executed via Dapper.
/// Completely bypasses Entity Framework Core change tracking and entity materialization.
/// </summary>
public interface INewsletterReadQueries
{
    /// <summary>
    /// Counts the total number of enabled newsletter subscriptions configured for the specified user.
    /// Used for dashboard counters and subscription limit validation.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>The total number of active subscriptions.</returns>
    Task<int> GetActiveSubscriptionCountByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the identifiers of all enabled newsletter subscriptions belonging to a user.
    /// Used for fast batch scheduling and worker pipeline queries.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>A read-only list of active newsletter subscription IDs.</returns>
    Task<IReadOnlyList<int>> GetActiveSubscriptionIdsByUserIdAsync(int userId, CancellationToken cancellationToken = default);
}
