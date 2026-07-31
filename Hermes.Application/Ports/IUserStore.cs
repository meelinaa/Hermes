using Hermes.Application.DTOs;
using Hermes.Domain.Entities;

namespace Hermes.Application.Ports;

public interface IUserStore
{
    Task SetUserAsync(User user, CancellationToken cancellationToken = default);
    Task<UserScope?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<UserScope?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserScope?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<User?> GetUserEntityForAuthenticationByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<User?> GetUserEntityForAuthenticationByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetUserEntityForAuthenticationByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<User?> GetUserEntityByIdAsync(int id, CancellationToken cancellationToken = default);
    Task UpdateUserAsync(User user, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(UserScope user, CancellationToken cancellationToken = default);

    Task SetUserEmailVerificationChallengeAsync(int userId, string verificationCode, DateTime expiresAtUtc, CancellationToken cancellationToken = default);

    Task CompleteUserEmailVerificationAsync(int userId, CancellationToken cancellationToken = default);
}
