using Hermes.Domain.DTOs;
using Hermes.Domain.Entities;

namespace Hermes.Application.Ports;

/// <summary>
/// Application-facing persistence port for Hermes use-cases.
/// Contains only business operations and no EF Core abstractions.
/// </summary>
public interface IHermesDataStore
{
    Task SetUserAsync(User user, CancellationToken cancellationToken = default);
    Task<UserScope?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<UserScope?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<UserScope?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<User?> GetUserEntityForAuthenticationByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<User?> GetUserEntityForAuthenticationByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetUserEntityForAuthenticationByIdAsync(int id, CancellationToken cancellationToken= default);
    Task<User?> GetUserEntityByIdAsync(int id, CancellationToken cancellationToken = default);
    Task UpdateUserAsync(User user, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(UserScope user, CancellationToken cancellationToken = default);
    Task SetNewsAsync(News news, CancellationToken cancellationToken = default);
    Task UpdateNewsAsync(News news, CancellationToken cancellationToken = default);
    Task DeleteNewsAsync(News news, CancellationToken cancellationToken = default);
    Task<List<News>> GetAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<List<NewsScheduleRow>> GetNewsScheduleRowsAsync(CancellationToken cancellationToken = default);
    Task<News?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default);
    Task<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default);
    Task SetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default);
    Task<NotificationLog?> GetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetActiveRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task CompleteRefreshRotationAsync(RefreshToken trackedOld, RefreshToken newToken, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenAsync(RefreshToken trackedToken, CancellationToken cancellationToken = default);
    Task RevokeAllRefreshTokensForUserAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsSentNotificationInWindowAsync(int userId, int newsId, DateTime windowStartUtc, DateTime windowEndUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a short-lived e-mail verification code and its UTC expiry on the user (<see cref="User.TwoFactorCode"/> / <see cref="User.TwoFactorExpiry"/>).
    /// </summary>
    Task SetUserEmailVerificationChallengeAsync(int userId, string verificationCode, DateTime expiresAtUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets <see cref="User.IsEmailVerified"/> to <c>true</c> and clears <see cref="User.TwoFactorCode"/> / <see cref="User.TwoFactorExpiry"/>.
    /// </summary>
    Task CompleteUserEmailVerificationAsync(int userId, CancellationToken cancellationToken = default);
}
