using Hermes.Application.DTOs.User;

namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// CQRS Read-Side interface for high-performance user queries executed via Dapper.
/// Completely bypasses Entity Framework Core change tracking and entity materialization.
/// </summary>
public interface IUserReadQueries
{
    /// <summary>
    /// Fetches lightweight user scope information by primary user identifier.
    /// Used for token claims hydration, authentication authorization, and profile headers.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>A <see cref="UserScopeDto"/> if found, or null.</returns>
    Task<UserScopeDto?> GetUserScopeByIdAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches lightweight user scope information by unique email address.
    /// Used for login, registration deduplication, and account lookups.
    /// </summary>
    /// <param name="email">The email address to search for.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>A <see cref="UserScopeDto"/> if found, or null.</returns>
    Task<UserScopeDto?> GetUserScopeByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches lightweight user scope information by username.
    /// Used for profile lookups and display resolution.
    /// </summary>
    /// <param name="name">The username to search for.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>A <see cref="UserScopeDto"/> if found, or null.</returns>
    Task<UserScopeDto?> GetUserScopeByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether an account with the specified email address already exists.
    /// Optimized single-row boolean existence query without pulling unnecessary columns.
    /// </summary>
    /// <param name="email">The email address to check.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the database query to complete.</param>
    /// <returns>True if a user with this email exists; otherwise false.</returns>
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
}
