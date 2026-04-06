using Hermes.Domain.Entities;

namespace Hermes.Application.Services;

/// <summary>Application operations for <see cref="User"/> persistence.</summary>
public interface IUserService
{
    Task RegisterUserAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateUserAsync(User user, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(User user, CancellationToken cancellationToken = default);
    Task<User?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<User?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);
}
