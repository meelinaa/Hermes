using FluentResults;
using Hermes.Application.DTOs.Login;
using Hermes.Application.DTOs.User;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Services.Users;

/// <summary>
/// Service implementation for user account registration, authentication via BCrypt password verification, credential updates, and session token invalidation.
/// </summary>
public sealed class UserAuthenticationService(
    IUserRepository db,
    IRefreshTokenRepository refreshTokenRepository) : IUserAuthenticationService
{
    /// <summary>
    /// Registers a new user account with BCrypt password hashing and persists it in the database.
    /// </summary>
    /// <param name="request">The registration request details containing username, email, and plain password.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A result containing the user scope DTO upon success, or validation/persistence errors on failure.</returns>
    public async Task<Result<UserScopeDto>> RegisterUserAsync(RegisterUserRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            return Result.Fail("Request cannot be null.");

        string name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            return Result.Fail("User name is required.");

        if (string.IsNullOrWhiteSpace(request.Email))
            return Result.Fail("User email is required.");
            
        Email email;
        try
        {
            email = Email.Parse(request.Email);
        }
        catch (DomainValidationException ex)
        {
            return Result.Fail(ex.Message);
        }
        
        UserScopeDto? existing = await db.GetUserByEmailAsync(email.Value, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
            return Result.Fail("A user with this email already exists.");
            
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password ?? "");
        
        User user;
        try
        {
            user = User.Create(name, email, passwordHash);
        }
        catch (DomainValidationException ex)
        {
            return Result.Fail(ex.Message);
        }
        
        await db.SetUserAsync(user, cancellationToken).ConfigureAwait(false);
        if (user.Id.Value <= 0)
            return Result.Fail("Failed to create user.");
        
        UserScopeDto userScope = new()
        {
            Name = user.Name,
            Email = user.Email.Value,
            UserId = user.Id.Value,
            IsEmailVerified = false
        };
        return Result.Ok(userScope);
    }

    /// <summary>
    /// Authenticates a user by username or email and validates the supplied password against the stored BCrypt hash.
    /// </summary>
    /// <param name="nameOrEmail">The user's username or email address.</param>
    /// <param name="password">The plain-text password to verify.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A result containing the login result DTO if credentials are valid, or an error if invalid.</returns>
    public async Task<Result<LoginResultDto>> LoginAsync(string nameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nameOrEmail))
            return Result.Fail("Name or email is required.");
        if (string.IsNullOrEmpty(password))
            return Result.Fail("Password is required.");

        string? key = nameOrEmail.Trim();
        User? user = key.Contains('@', StringComparison.Ordinal)
            ? await db.GetUserEntityForAuthenticationByEmailAsync(key, cancellationToken).ConfigureAwait(false)
            : await db.GetUserEntityForAuthenticationByNameAsync(key, cancellationToken).ConfigureAwait(false);

        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
            return Result.Fail("Invalid login or password.");

        bool valid;
        try
        {
            valid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        }
        catch (Exception ex) when (ex is BCrypt.Net.SaltParseException or ArgumentException)
        {
            valid = false;
        }

        if (!valid)
            return Result.Fail("Invalid login or password.");

        return Result.Ok(new LoginResultDto(true, null, user.Id.Value, user.Email.Value, user.Name));
    }

    /// <summary>
    /// Updates user account details including name, email, and password. Revokes all active refresh tokens if the password is changed.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to update.</param>
    /// <param name="name">The new display name.</param>
    /// <param name="email">The new primary email address.</param>
    /// <param name="newPasswordPlain">Optional new plain password.</param>
    /// <param name="currentPasswordPlain">The current plain password, required if setting a new password.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A result indicating success or detailing validation/authentication failures.</returns>
    public async Task<Result> UpdateUserAsync(int userId, string name, string email, string? newPasswordPlain, string? currentPasswordPlain = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Fail("Name is required.");
        if (string.IsNullOrWhiteSpace(email))
            return Result.Fail("Email is required.");

        UserId uid = new UserId(userId);
        User? existing = await db.GetUserEntityByIdAsync(uid, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            return Result.Fail($"User with id {userId} was not found.");

        try
        {
            existing.Rename(name);
            existing.ChangePrimaryEmail(Email.Parse(email));
        }
        catch (DomainValidationException ex)
        {
            return Result.Fail(ex.Message);
        }

        bool passwordChanged = false;
        if (!string.IsNullOrWhiteSpace(newPasswordPlain))
        {
            if (string.IsNullOrWhiteSpace(currentPasswordPlain))
                return Result.Fail("Current password is required when setting a new password.");

            if (string.IsNullOrEmpty(existing.PasswordHash))
                return Result.Fail("Cannot change password: no password is set for this account.");

            bool valid;
            try
            {
                valid = BCrypt.Net.BCrypt.Verify(currentPasswordPlain.Trim(), existing.PasswordHash);
            }
            catch (Exception ex) when (ex is BCrypt.Net.SaltParseException or ArgumentException)
            {
                valid = false;
            }

            if (!valid)
                return Result.Fail("Current password verification failed.");

            try
            {
                existing.ReplacePasswordHash(BCrypt.Net.BCrypt.HashPassword(newPasswordPlain.Trim()));
                passwordChanged = true;
            }
            catch (DomainValidationException ex)
            {
                return Result.Fail(ex.Message);
            }
        }

        await db.UpdateUserAsync(existing, cancellationToken).ConfigureAwait(false);

        if (passwordChanged)
        {
            await refreshTokenRepository.RevokeAllRefreshTokensForUserAsync(uid, cancellationToken).ConfigureAwait(false);
        }

        return Result.Ok();
    }
}
