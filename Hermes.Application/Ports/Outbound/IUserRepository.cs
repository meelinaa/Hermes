using Hermes.Application.DTOs.User;
using Hermes.Domain.Entities;
using Hermes.Domain.ValueObjects;

namespace Hermes.Application.Ports.Outbound;

public interface IUserRepository
{
    ValueTask SetUserAsync(User user, CancellationToken cancellationToken = default);
    ValueTask<UserScopeDto?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default);
    ValueTask<UserScopeDto?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    ValueTask<UserScopeDto?> GetUserByIdAsync(UserId id, CancellationToken cancellationToken = default);
    ValueTask<User?> GetUserEntityForAuthenticationByNameAsync(string name, CancellationToken cancellationToken = default);
    ValueTask<User?> GetUserEntityForAuthenticationByEmailAsync(string email, CancellationToken cancellationToken = default);
    ValueTask<User?> GetUserEntityForAuthenticationByIdAsync(UserId id, CancellationToken cancellationToken = default);
    ValueTask<User?> GetUserEntityByIdAsync(UserId id, CancellationToken cancellationToken = default);
    ValueTask UpdateUserAsync(User user, CancellationToken cancellationToken = default);
    ValueTask DeleteUserAsync(UserScopeDto user, CancellationToken cancellationToken = default);

    ValueTask SetUserEmailVerificationChallengeAsync(UserId userId, string verificationCode, DateTime expiresAtUtc, CancellationToken cancellationToken = default);

    ValueTask CompleteUserEmailVerificationAsync(UserId userId, CancellationToken cancellationToken = default);
}
