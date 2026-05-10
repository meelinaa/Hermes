using Hermes.Domain.DTOs;
using Hermes.Domain.Entities;

namespace Hermes.Application.Ports;

/// <summary>User persistence for application use-cases (no EF-specific types).</summary>
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

    /// <summary>
    /// Persists a short-lived e-mail verification code and its UTC expiry on the user (<see cref="User.TwoFactorCode"/> / <see cref="User.TwoFactorExpiry"/>).
    /// </summary>
    Task SetUserEmailVerificationChallengeAsync(int userId, string verificationCode, DateTime expiresAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets <see cref="User.IsEmailVerified"/> to <c>true</c> and clears <see cref="User.TwoFactorCode"/> / <see cref="User.TwoFactorExpiry"/>.
    /// </summary>
    Task CompleteUserEmailVerificationAsync(int userId, CancellationToken cancellationToken = default);
}
