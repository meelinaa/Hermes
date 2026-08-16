using Hermes.Application.DTOs.User;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Outbound;

/// <summary>
/// Outbound port for user profile CRUD, retrieval, and identity entity persistence.
/// </summary>
public interface IUserStore
{
    /// <summary>
    /// Inserts a newly created user into the persistence store.
    /// </summary>
    /// <param name="user">The user entity to persist.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    ValueTask SetUserAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves user scope information matching the specified username.
    /// </summary>
    /// <param name="name">The username to search for.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>The matching user scope DTO, or null if not found.</returns>
    ValueTask<UserScopeDto?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves user scope information matching the specified email address.
    /// </summary>
    /// <param name="email">The email address to search for.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>The matching user scope DTO, or null if not found.</returns>
    ValueTask<UserScopeDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves user scope information matching the specified unique user ID.
    /// </summary>
    /// <param name="id">The user ID to search for.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>The matching user scope DTO, or null if not found.</returns>
    ValueTask<UserScopeDto?> GetUserByIdAsync(UserId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the user entity by ID for state modifications or profile updates.
    /// </summary>
    /// <param name="id">The user ID to search for.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>The matching user domain entity, or null if not found.</returns>
    ValueTask<User?> GetUserEntityByIdAsync(UserId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists updated user profile fields to the database.
    /// </summary>
    /// <param name="user">The modified user entity.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    ValueTask UpdateUserAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user account and triggers cascading removal of associated subscriptions and tokens.
    /// </summary>
    /// <param name="user">The user scope DTO representing the account to delete.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    ValueTask DeleteUserAsync(UserScopeDto user, CancellationToken cancellationToken = default);
}
