using Hermes.Application.DTOs.Login;
using Hermes.Application.DTOs.User;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Services.Users;

/// <summary>
/// Service implementation for user account registration, authentication via BCrypt password verification, and credential updates.
/// </summary>
public sealed class UserAuthenticationService(IUserRepository db) : IUserAuthenticationService
{
    /// <summary>
    /// Registers a new user account by validating input, normalizing email format, hashing the plain password with BCrypt, and persisting the user record.
    /// </summary>
    /// <param name="request">The registration DTO containing user display name, email, and plain text password.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A <see cref="UserScopeDto"/> containing the newly created user's basic profile details.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when name/email is missing or persistence fails.</exception>
    public async Task<UserScopeDto> RegisterUserAsync(RegisterUserRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("User name is required.");

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new InvalidOperationException("User email is required.");
        Email email = Email.Parse(request.Email);
        
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password ?? "");
        
        User user = User.Create(name, email, passwordHash);
        
        await db.SetUserAsync(user, cancellationToken).ConfigureAwait(false);
        if (user.Id.Value <= 0)
            throw new InvalidOperationException("Failed to create user.");
        
        UserScopeDto userScope = new()
        {
            Name = user.Name,
            Email = user.Email.Value,
            UserId = user.Id.Value,
            IsEmailVerified = false
        };
        return userScope;
    }

    /// <summary>
    /// Authenticates a user by username or email address and verifies the provided password against the BCrypt hash stored in the repository.
    /// </summary>
    /// <param name="nameOrEmail">The user's account display name or email address.</param>
    /// <param name="password">The plain text password attempt.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <returns>A <see cref="LoginResultDto"/> indicating authentication success or failure with generic error message.</returns>
    public async Task<LoginResultDto> LoginAsync(string nameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nameOrEmail))
            return new LoginResultDto(false, "Name or email is required.", null);
        if (string.IsNullOrEmpty(password))
            return new LoginResultDto(false, "Password is required.", null);

        string? key = nameOrEmail.Trim();
        User? user = key.Contains('@', StringComparison.Ordinal)
            ? await db.GetUserEntityForAuthenticationByEmailAsync(key, cancellationToken).ConfigureAwait(false)
            : await db.GetUserEntityForAuthenticationByNameAsync(key, cancellationToken).ConfigureAwait(false);

        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
            return new LoginResultDto(false, "Invalid login or password.", null);

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
            return new LoginResultDto(false, "Invalid login or password.", null);

        return new LoginResultDto(true, null, user.Id.Value, user.Email.Value, user.Name);
    }

    /// <summary>
    /// Updates user account details (name, email) and optionally changes password after verifying current credentials.
    /// </summary>
    /// <param name="user">The updated user entity.</param>
    /// <param name="currentPasswordPlain">Optional current password required when changing to a new password.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the async operation to complete.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="user"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when required fields or current password are missing.</exception>
    /// <exception cref="UserNotFoundException">Thrown when user ID is not found.</exception>
    /// <exception cref="WrongCurrentPasswordException">Thrown when current password verification fails.</exception>
    public async Task UpdateUserAsync(int userId, string name, string email, string? newPasswordPlain, string? currentPasswordPlain = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        UserId uid = new UserId(userId);
        User? existing = await db.GetUserEntityByIdAsync(uid, cancellationToken).ConfigureAwait(false);
        if (existing is null)
            throw new UserNotFoundException($"User with id {userId} was not found.");

        existing.Rename(name);
        existing.ChangePrimaryEmail(Email.Parse(email));

        if (!string.IsNullOrWhiteSpace(newPasswordPlain))
        {
            if (string.IsNullOrWhiteSpace(currentPasswordPlain))
                throw new ArgumentException("Current password is required when setting a new password.", nameof(currentPasswordPlain));

            if (string.IsNullOrEmpty(existing.PasswordHash))
                throw new InvalidOperationException("Cannot change password: no password is set for this account.");

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
                throw new WrongCurrentPasswordException();

            existing.ReplacePasswordHash(BCrypt.Net.BCrypt.HashPassword(newPasswordPlain.Trim()));
        }

        await db.UpdateUserAsync(existing, cancellationToken).ConfigureAwait(false);
    }
}
