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
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("User name is required.");
        request.Name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new InvalidOperationException("User email is required.");
        Email email = Email.Parse(request.Email);
        request.Email = email.Value;
        request.Password = BCrypt.Net.BCrypt.HashPassword(request.Password ?? "");
        User user = new()
        {
            Name = request.Name,
            Email = request.Email,
            PasswordHash = request.Password
        };
        await db.SetUserAsync(user, cancellationToken).ConfigureAwait(false);
        if (user.Id <= 0)
            throw new InvalidOperationException("Failed to create user.");
        UserScopeDto userScope = new()
        {
            Name = user.Name,
            Email = user.Email,
            UserId = user.Id,
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

        return new LoginResultDto(true, null, user.Id, user.Email, user.Name);
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
    public async Task UpdateUserAsync(User user, string? currentPasswordPlain = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrEmpty(user.Name))
            throw new ArgumentException("Name is required.", nameof(user));
        if (string.IsNullOrEmpty(user.Email))
            throw new ArgumentException("Email is required.", nameof(user));

        Email normalizedEmail = Email.Parse(user.Email);
        user.Email = normalizedEmail.Value;

        string? newPlain = user.PasswordHash;
        string? hashedForDb = null;
        if (!string.IsNullOrWhiteSpace(newPlain))
        {
            if (string.IsNullOrWhiteSpace(currentPasswordPlain))
                throw new ArgumentException("Current password is required when setting a new password.", nameof(currentPasswordPlain));

            User? existing = await db.GetUserEntityByIdAsync(user.Id, cancellationToken).ConfigureAwait(false);
            if (existing is null)
                throw new UserNotFoundException($"User with id {user.Id} was not found.");
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

            hashedForDb = BCrypt.Net.BCrypt.HashPassword(newPlain.Trim());
        }

        user.PasswordHash = hashedForDb;
        await db.UpdateUserAsync(user, cancellationToken).ConfigureAwait(false);
    }
}
