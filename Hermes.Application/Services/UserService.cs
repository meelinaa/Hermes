using Hermes.Application.DTOs.User;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;

namespace Hermes.Application.Services;

public sealed class UserService(IUserRepository db) : IUserService
{
    public async Task DeleteUserAsync(UserScopeDto user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await db.DeleteUserAsync(user, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserScopeDto?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be null or whitespace.", nameof(name));
        return await db.GetUserByNameAsync(name, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserScopeDto?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentException("Id must be greater than zero.", nameof(id));
        return await db.GetUserByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<UserScopeDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be null or whitespace.", nameof(email));
        return await db.GetUserByEmailAsync(email, cancellationToken).ConfigureAwait(false);
    }
}
