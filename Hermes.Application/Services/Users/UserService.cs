using FluentResults;
using Hermes.Application.DTOs.User;
using Hermes.Application.Errors;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Services.Users;

/// <summary>
/// Service implementation for querying user profiles and executing user account deletion operations.
/// Follows Interface Segregation by depending strictly on <see cref="IUserStore"/>.
/// </summary>
public sealed class UserService(IUserStore db) : IUserService
{
    /// <summary>
    /// Deletes a user account and purges associated resources from the system.
    /// </summary>
    /// <param name="user">The user scope DTO identifying the user to delete.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A Result indicating success or validation failure.</returns>
    public async ValueTask<Result> DeleteUserAsync(UserScopeDto user, CancellationToken cancellationToken = default)
    {
        if (user is null)
            return Result.Fail(new ValidationError("User is required."));
            
        await db.DeleteUserAsync(user, cancellationToken).ConfigureAwait(false);
        return Result.Ok();
    }

    /// <summary>
    /// Looks up a user account by its display name.
    /// </summary>
    /// <param name="name">The display name to search for.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A Result containing the user scope DTO or a not-found error.</returns>
    public async ValueTask<Result<UserScopeDto>> GetUserByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Fail(new ValidationError("Name cannot be null or whitespace."));
            
        UserScopeDto? user = await db.GetUserByNameAsync(name, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return Result.Fail(new UserNotFoundError(name, isEmail: false));
            
        return Result.Ok(user);
    }

    /// <summary>
    /// Looks up a user account by its unique identifier.
    /// </summary>
    /// <param name="id">The unique user identifier.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A Result containing the user scope DTO or a not-found error.</returns>
    public async ValueTask<Result<UserScopeDto>> GetUserByIdAsync(UserId id, CancellationToken cancellationToken = default)
    {
        if (id.Value <= 0)
            return Result.Fail(new ValidationError("Id must be greater than zero."));
            
        UserScopeDto? user = await db.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return Result.Fail(new UserNotFoundError(id.Value));
            
        return Result.Ok(user);
    }

    /// <summary>
    /// Looks up a user account by its email address.
    /// </summary>
    /// <param name="email">The email address to search for.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A Result containing the user scope DTO or a not-found error.</returns>
    public async ValueTask<Result<UserScopeDto>> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result.Fail(new ValidationError("Email cannot be null or whitespace."));
            
        UserScopeDto? user = await db.GetUserByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return Result.Fail(new UserNotFoundError(email, isEmail: true));
            
        return Result.Ok(user);
    }
}
