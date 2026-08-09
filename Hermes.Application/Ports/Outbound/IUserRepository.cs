using Hermes.Application.DTOs.User;
using Hermes.Domain.Entities;

namespace Hermes.Application.Ports.Outbound;

public interface IUserRepository
{
    ValueTask SetUserAsync(User user, CancellationToken cancellationToken = default);
    ValueTask<UserScopeDto?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default);
    ValueTask<UserScopeDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    ValueTask<UserScopeDto?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);
    ValueTask<User?> GetUserEntityForAuthenticationByNameAsync(string name, CancellationToken cancellationToken = default);
    ValueTask<User?> GetUserEntityForAuthenticationByEmailAsync(string email, CancellationToken cancellationToken = default);
    ValueTask<User?> GetUserEntityForAuthenticationByIdAsync(int id, CancellationToken cancellationToken = default);
    ValueTask<User?> GetUserEntityByIdAsync(int id, CancellationToken cancellationToken = default);
    ValueTask UpdateUserAsync(User user, CancellationToken cancellationToken = default);
    ValueTask DeleteUserAsync(UserScopeDto user, CancellationToken cancellationToken = default);

    ValueTask SetUserEmailVerificationChallengeAsync(int userId, string verificationCode, DateTime expiresAtUtc, CancellationToken cancellationToken = default);

    ValueTask CompleteUserEmailVerificationAsync(int userId, CancellationToken cancellationToken = default);
}
