using Hermes.Application.DTOs.User;
using Hermes.Domain.Entities;

namespace Hermes.Application.Ports.Outbound;

public interface IUserRepository
{
    Task SetUserAsync(User user, CancellationToken cancellationToken = default);
    Task<UserScopeDto?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<UserScopeDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserScopeDto?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<User?> GetUserEntityForAuthenticationByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<User?> GetUserEntityForAuthenticationByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetUserEntityForAuthenticationByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<User?> GetUserEntityByIdAsync(int id, CancellationToken cancellationToken = default);
    Task UpdateUserAsync(User user, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(UserScopeDto user, CancellationToken cancellationToken = default);

    Task SetUserEmailVerificationChallengeAsync(int userId, string verificationCode, DateTime expiresAtUtc, CancellationToken cancellationToken = default);

    Task CompleteUserEmailVerificationAsync(int userId, CancellationToken cancellationToken = default);
}
