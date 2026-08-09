using Hermes.Application.DTOs.User;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;

namespace Hermes.Application.Services.Users;

/// <summary>
/// Service implementation for querying user profiles and executing user account deletion operations.
/// </summary>
public sealed class UserService(IUserRepository db) : IUserService
{
    /// <summary>
    /// Deletes a specific user account profile from the persistent database store.
    /// </summary>
    /// <param name="user">The user scope DTO identifying the user to delete.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="user"/> is null.</exception>
    public async ValueTask DeleteUserAsync(UserScopeDto user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await db.DeleteUserAsync(user, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves user scope details by display name.
    /// </summary>
    /// <param name="name">The account display name to search for.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A <see cref="UserScopeDto"/> if found; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public async ValueTask<UserScopeDto?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or whitespace.", nameof(name));
        return await db.GetUserByNameAsync(name, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves user scope details by unique user identifier.
    /// </summary>
    /// <param name="id">The positive integer user ID.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A <see cref="UserScopeDto"/> if found; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is non-positive.</exception>
    public async ValueTask<UserScopeDto?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("Id must be greater than zero.", nameof(id));
        return await db.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Retrieves user scope details by email address.
    /// </summary>
    /// <param name="email">The account email address to search for.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A <see cref="UserScopeDto"/> if found; otherwise, <c>null</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="email"/> is null or whitespace.</exception>
    public async ValueTask<UserScopeDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or whitespace.", nameof(email));
        return await db.GetUserByEmailAsync(email, cancellationToken).ConfigureAwait(false);
    }
}
