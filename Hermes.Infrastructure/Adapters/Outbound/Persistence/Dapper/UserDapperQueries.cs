using System.Data;
using Dapper;
using Hermes.Application.DTOs.User;
using Hermes.Application.Ports.Outbound;

namespace Hermes.Infrastructure.Adapters.Outbound.Persistence.Dapper;

/// <summary>
/// High-performance Dapper implementation of <see cref="IUserReadQueries"/>.
/// Executes raw, parameterized SQL queries to bypass EF Core tracking and materialization overhead.
/// </summary>
public sealed class UserDapperQueries(ISqlConnectionFactory connectionFactory) : IUserReadQueries
{
    /// <summary>
    /// Fetches lightweight user summary scope by user ID using a direct indexed SQL lookup.
    /// </summary>
    /// <param name="userId">The primary key ID of the user.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>A <see cref="UserScopeDto"/> if found, or null.</returns>
    public async Task<UserScopeDto?> GetUserScopeByIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT Id AS UserId, Name, Email, IsEmailVerified
            FROM users
            WHERE Id = @UserId
            LIMIT 1;";

        using IDbConnection connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<UserScopeDto>(command).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches lightweight user summary scope by unique email address using a direct indexed SQL lookup.
    /// </summary>
    /// <param name="email">The email address to search for.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>A <see cref="UserScopeDto"/> if found, or null.</returns>
    public async Task<UserScopeDto?> GetUserScopeByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        const string sql = @"
            SELECT Id AS UserId, Name, Email, IsEmailVerified
            FROM users
            WHERE Email = @Email
            LIMIT 1;";

        using IDbConnection connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(sql, new { Email = email.Trim().ToLowerInvariant() }, cancellationToken: cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<UserScopeDto>(command).ConfigureAwait(false);
    }

    /// <summary>
    /// Fetches lightweight user summary scope by username using a direct SQL lookup.
    /// </summary>
    /// <param name="name">The username to search for.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>A <see cref="UserScopeDto"/> if found, or null.</returns>
    public async Task<UserScopeDto?> GetUserScopeByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        const string sql = @"
            SELECT Id AS UserId, Name, Email, IsEmailVerified
            FROM users
            WHERE Name = @Name
            LIMIT 1;";

        using IDbConnection connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(sql, new { Name = name.Trim() }, cancellationToken: cancellationToken);
        return await connection.QueryFirstOrDefaultAsync<UserScopeDto>(command).ConfigureAwait(false);
    }

    /// <summary>
    /// Checks whether an account exists for the given email address using an optimized SQL EXISTS query.
    /// </summary>
    /// <param name="email">The email address to check.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>True if a user exists with the given email; otherwise false.</returns>
    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        const string sql = @"
            SELECT EXISTS(
                SELECT 1
                FROM users
                WHERE Email = @Email
            );";

        using IDbConnection connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var command = new CommandDefinition(sql, new { Email = email.Trim().ToLowerInvariant() }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(command).ConfigureAwait(false);
    }
}
