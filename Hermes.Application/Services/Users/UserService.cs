using FluentResults;
using Hermes.Application.DTOs.User;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Services.Users;

/// <summary>
/// Service implementation for querying user profiles and executing user account deletion operations.
/// </summary>
public sealed class UserService(IUserRepository db) : IUserService
{
    public async ValueTask<Result> DeleteUserAsync(UserScopeDto user, CancellationToken cancellationToken = default)
    {
        if (user is null)
            return Result.Fail("User is required.");
            
        await db.DeleteUserAsync(user, cancellationToken).ConfigureAwait(false);
        return Result.Ok();
    }

    public async ValueTask<Result<UserScopeDto>> GetUserByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Fail("Name cannot be null or whitespace.");
            
        UserScopeDto? user = await db.GetUserByNameAsync(name, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return Result.Fail($"User with name '{name}' not found.");
            
        return Result.Ok(user);
    }

    public async ValueTask<Result<UserScopeDto>> GetUserByIdAsync(UserId id, CancellationToken cancellationToken = default)
    {
        if (id.Value <= 0)
            return Result.Fail("Id must be greater than zero.");
            
        UserScopeDto? user = await db.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return Result.Fail($"User with id '{id.Value}' not found.");
            
        return Result.Ok(user);
    }

    public async ValueTask<Result<UserScopeDto>> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result.Fail("Email cannot be null or whitespace.");
            
        UserScopeDto? user = await db.GetUserByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return Result.Fail($"User with email '{email}' not found.");
            
        return Result.Ok(user);
    }
}
