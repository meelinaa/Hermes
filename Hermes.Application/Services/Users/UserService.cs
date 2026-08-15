using FluentResults;
using Hermes.Application.DTOs.User;
using Hermes.Application.Errors;
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
            return Result.Fail(new ValidationError("User is required."));
            
        await db.DeleteUserAsync(user, cancellationToken).ConfigureAwait(false);
        return Result.Ok();
    }

    public async ValueTask<Result<UserScopeDto>> GetUserByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Fail(new ValidationError("Name cannot be null or whitespace."));
            
        UserScopeDto? user = await db.GetUserByNameAsync(name, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return Result.Fail(new UserNotFoundError(name, isEmail: false));
            
        return Result.Ok(user);
    }

    public async ValueTask<Result<UserScopeDto>> GetUserByIdAsync(UserId id, CancellationToken cancellationToken = default)
    {
        if (id.Value <= 0)
            return Result.Fail(new ValidationError("Id must be greater than zero."));
            
        UserScopeDto? user = await db.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null)
            return Result.Fail(new UserNotFoundError(id.Value));
            
        return Result.Ok(user);
    }

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
