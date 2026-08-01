using Hermes.Application.DTOs.Login;
using Hermes.Application.DTOs.User;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.Exceptions;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Services;

public sealed class UserAuthenticationService(IUserRepository db) : IUserAuthenticationService
{
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
        catch
        {
            valid = false;
        }

        if (!valid)
            return new LoginResultDto(false, "Invalid login or password.", null);

        return new LoginResultDto(true, null, user.Id, user.Email, user.Name);
    }

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
            catch
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
