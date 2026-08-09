using Hermes.Application.DTOs.User;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using EmailAddress = Hermes.Domain.ValueObjects.Email;

namespace Hermes.Infrastructure.Adapters.Outbound.Repositories;

/// <inheritdoc />
public sealed class UserRepository(HermesDbContext db) : IUserRepository
{
    /// <inheritdoc />
    public async ValueTask SetUserAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id != 0)
            throw new ArgumentException("New users must have id 0 before insert.", nameof(user));

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            EmailAddress email = EmailAddress.Parse(user.Email);
            user.Email = email.Value;
            bool exists = await db.Users.AsNoTracking()
                .AnyAsync(userEntity => userEntity.Email == email.Value, cancellationToken)
                .ConfigureAwait(false);
            if (exists)
                throw new EmailAlreadyExistsException();
        }

        await db.Users.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<UserScopeDto?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        User? user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(userEntity => userEntity.Name == name, cancellationToken)
            .ConfigureAwait(false);

        return user is null ? throw new UserNotFoundException($"User with name '{name}' was not found.") : MapToUserScope(user);
    }

    /// <inheritdoc />
    public async ValueTask<UserScopeDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.", nameof(email));

        EmailAddress normalized = EmailAddress.Parse(email);
        User? user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(userEntity => userEntity.Email != null && userEntity.Email == normalized.Value, cancellationToken)
            .ConfigureAwait(false);

        return user is null ? throw new UserNotFoundException($"User with email '{email}' was not found.") : MapToUserScope(user);
    }

    /// <inheritdoc />
    public async ValueTask<UserScopeDto?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), id, "User id must be greater than zero.");

        User? user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(userEntity => userEntity.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return user is null ? throw new UserNotFoundException($"User with id '{id}' was not found.") : MapToUserScope(user);
    }

    /// <inheritdoc />
    public async ValueTask<User?> GetUserEntityForAuthenticationByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        User? user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(userEntity => userEntity.Name == name, cancellationToken)
            .ConfigureAwait(false);
        return user is null ? throw new UserNotFoundException() : user;
    }

    /// <inheritdoc />
    public async ValueTask<User?> GetUserEntityForAuthenticationByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        EmailAddress normalized = EmailAddress.Parse(email);

        User? user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(userEntity => userEntity.Email == normalized.Value, cancellationToken)
            .ConfigureAwait(false);

        return user ?? throw new UserNotFoundException();
    }

    /// <inheritdoc />
    public async ValueTask<User?> GetUserEntityForAuthenticationByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), id, "User id must be greater than zero.");

        User? user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(userEntity => userEntity.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return user ?? throw new UserNotFoundException();
    }

    /// <inheritdoc />
    public async ValueTask<User?> GetUserEntityByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;
        return await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(userEntity => userEntity.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask UpdateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id <= 0)
            throw new ArgumentException("User id must be greater than zero for update.", nameof(user));

        User? entity = await db.Users.FirstOrDefaultAsync(userEntity => userEntity.Id == user.Id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            throw new UserNotFoundException($"User with id {user.Id} was not found.");

        entity.Rename(user.Name!);
        entity.ChangePrimaryEmail(EmailAddress.Parse(user.Email!));

        if (!string.IsNullOrWhiteSpace(user.PasswordHash))
            entity.ReplacePasswordHash(user.PasswordHash!);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DeleteUserAsync(UserScopeDto user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.UserId <= 0)
            throw new ArgumentException("User id must be greater than zero.", nameof(user));

        bool exists = await db.Users.AsNoTracking()
            .AnyAsync(userEntity => userEntity.Id == user.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new UserNotFoundException($"User with id {user.UserId} was not found.");

        User userEntity = MapToUserEntity(user);
        db.Users.Remove(userEntity);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SetUserEmailVerificationChallengeAsync(
        int userId,
        string verificationCode,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), userId, "User id must be greater than zero.");
        if (string.IsNullOrWhiteSpace(verificationCode))
            throw new ArgumentException("Verification code is required.", nameof(verificationCode));

        DateTime expires = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc);
        User? user = await db.Users.FirstOrDefaultAsync(userEntity => userEntity.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
            throw new UserNotFoundException($"User with id {userId} was not found.");

        user.TwoFactorCode = verificationCode.Trim();
        user.TwoFactorExpiry = expires;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask CompleteUserEmailVerificationAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), userId, "User id must be greater than zero.");

        User? user = await db.Users.FirstOrDefaultAsync(userEntity => userEntity.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
            throw new UserNotFoundException($"User with id {userId} was not found.");

        user.IsEmailVerified = true;
        user.TwoFactorCode = null;
        user.TwoFactorExpiry = null;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static UserScopeDto MapToUserScope(User user) => new()
    {
        UserId = user.Id,
        Name = user.Name ?? string.Empty,
        Email = user.Email ?? string.Empty,
        IsEmailVerified = user.IsEmailVerified
    };

    private static User MapToUserEntity(UserScopeDto scope) => new()
    {
        Id = scope.UserId,
        Name = scope.Name,
        Email = scope.Email
    };
}
