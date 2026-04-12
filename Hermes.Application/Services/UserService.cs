using Hermes.Application.Models;
using Hermes.Domain.DTOs;
using Hermes.Domain.Entities;
using Hermes.Domain.Interfaces.DBContext;
using Hermes.Domain.Interfaces.Services;

namespace Hermes.Application.Services;

public sealed class UserService(IHermesDbContext db) : IUserService
{
    public async Task<UserScope> RegisterUserAsync(User user, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(user.Email))
            user.Email = user.Email.Trim().ToLowerInvariant();
        // API sends plain password in PasswordHash field; store BCrypt hash only.
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash ?? "");
        await db.SetUserAsync(user, cancellationToken).ConfigureAwait(false);
        if (user.Id <= 0)
            throw new InvalidOperationException("Failed to create user.");
        if (string.IsNullOrEmpty(user.Name))
            throw new InvalidOperationException("User name is required.");
        if (string.IsNullOrEmpty(user.Email))
            throw new InvalidOperationException("User email is required.");
        UserScope userScope = new()
        {
            Name = user.Name,
            Email = user.Email,
            UserId = user.Id
        };
        return userScope;
    }

    public async Task<LoginResult> LoginAsync(string nameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nameOrEmail))
            return new LoginResult(false, "Name or email is required.", null);
        if (string.IsNullOrEmpty(password))
            return new LoginResult(false, "Password is required.", null);

        var key = nameOrEmail.Trim();
        User? user;
        if (key.Contains('@', StringComparison.Ordinal))
            user = await db.GetUserEntityForAuthenticationByEmailAsync(key, cancellationToken).ConfigureAwait(false);
        else
            user = await db.GetUserEntityForAuthenticationByNameAsync(key, cancellationToken).ConfigureAwait(false);

        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
            return new LoginResult(false, "Invalid login or password.", null);

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
            return new LoginResult(false, "Invalid login or password.", null);

        return new LoginResult(true, null, user.Id, user.Email, user.Name);
    }

    public async Task UpdateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrEmpty(user.Name))
            throw new ArgumentException("Name is required.", nameof(user));
        await db.UpdateUserAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteUserAsync(UserScope user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await db.DeleteUserAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserScope?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or whitespace.", nameof(name));
        return await db.GetUserByNameAsync(name, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserScope?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("Id must be greater than zero.", nameof(id));
        return await db.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserScope?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or whitespace.", nameof(email));
        return await db.GetUserByEmailAsync(email, cancellationToken).ConfigureAwait(false);
    }
}
