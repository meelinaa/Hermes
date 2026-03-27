using Hermes.Domain.Entities;

namespace Hermes.Infrastructure.Repositories.Interfaces;

public interface IUserRepository
{
    Task<User?> GetUserByIdAsync(int id, CancellationToken ct = default);
    Task<User?> GetUserByEmailAsync(string email, CancellationToken ct = default);
    Task CreateUserAsync(User user, CancellationToken ct = default);
    Task UpdateUserAsync(User user, CancellationToken ct = default);
    Task DeleteUserAsync(int id, CancellationToken ct = default);
}
