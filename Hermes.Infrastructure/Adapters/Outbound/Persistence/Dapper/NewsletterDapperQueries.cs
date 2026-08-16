using System.Data;
using Dapper;
using Hermes.Application.Ports.Outbound;

namespace Hermes.Infrastructure.Adapters.Outbound.Persistence.Dapper;

/// <summary>
/// High-performance Dapper implementation of <see cref="INewsletterReadQueries"/>.
/// Executes raw, parameterized SQL queries for rapid aggregate lookups.
/// </summary>
public sealed class NewsletterDapperQueries(ISqlConnectionFactory connectionFactory) : INewsletterReadQueries
{
    /// <summary>
    /// Counts the total number of enabled newsletter subscriptions for a user via direct COUNT SQL.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>The count of active newsletter subscriptions.</returns>
    public async Task<int> GetActiveSubscriptionCountByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT COUNT(*)
            FROM news
            WHERE UserId = @UserId AND IsEnabled = 1;";

        using IDbConnection connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(command).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves a list of subscription IDs for all enabled newsletters belonging to a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>A read-only list of active newsletter subscription IDs.</returns>
    public async Task<IReadOnlyList<int>> GetActiveSubscriptionIdsByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id
            FROM news
            WHERE UserId = @UserId AND IsEnabled = 1
            ORDER BY Id ASC;";

        using IDbConnection connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken);
        var ids = await connection.QueryAsync<int>(command).ConfigureAwait(false);
        return ids.ToList().AsReadOnly();
    }
}
