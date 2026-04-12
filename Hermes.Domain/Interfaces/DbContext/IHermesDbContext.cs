using Hermes.Domain.DTOs;
using Hermes.Domain.Entities;
using Hermes.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Hermes.Domain.Interfaces.DBContext
{
    /// <summary>
    /// Abstraction for Hermes persistence: users, news, and notification logs.
    /// </summary>
    public interface IHermesDbContext
    {
        /// <summary>
        /// Users stored in the database.
        /// </summary>
        DbSet<User> Users { get; }

        /// <summary>
        /// Per-user news subscription settings.
        /// </summary>
        DbSet<News> News { get; }

        /// <summary>
        /// Notification delivery log entries.
        /// </summary>
        DbSet<NotificationLog> NotificationLogs { get; }

        /// <summary>
        /// Inserts a new user and saves changes immediately (single unit of work).
        /// </summary>
        /// <param name="user">The user entity to insert.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>For application-layer code, prefer <see cref="IUserRepository.CreateUserAsync"/>.</remarks>
        Task SetUserAsync(User user, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the first user whose display name matches <paramref name="name"/>, or <c>null</c> if none match.
        /// </summary>
        /// <param name="name">Name value to match.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The matching user, or <c>null</c>.</returns>
        Task<UserScope?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the user with the given email (trimmed), or <c>null</c> if not found.
        /// </summary>
        /// <param name="email">Email address to match.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The matching user, or <c>null</c>.</returns>
        Task<UserScope?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the user with the given primary key, or <c>null</c> if not found.
        /// </summary>
        /// <param name="id">User identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The user, or <c>null</c>.</returns>
        Task<UserScope?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads the full user entity for credential verification (includes password hash). Do not expose in API responses.
        /// </summary>
        Task<User?> GetUserEntityForAuthenticationByNameAsync(string name, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads the full user entity for credential verification (includes password hash). Do not expose in API responses.
        /// </summary>
        Task<User?> GetUserEntityForAuthenticationByEmailAsync(string email, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks the user as modified and saves changes immediately.
        /// </summary>
        /// <param name="user">The user entity to update.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task UpdateUserAsync(User user, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the user from the database and saves changes immediately.
        /// </summary>
        /// <param name="user">The user entity to delete (must be tracked or known to the context).</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task DeleteUserAsync(UserScope user, CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts a news settings row and saves changes immediately.
        /// </summary>
        /// <param name="news">The news entity to insert.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetNewsAsync(News news, CancellationToken cancellationToken = default);

        /// <summary>
        /// Marks the news row as modified and saves changes immediately.
        /// </summary>
        /// <param name="news">The news entity to update.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task UpdateNewsAsync(News news, CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes the news row and saves changes immediately.
        /// </summary>
        /// <param name="news">The news entity to delete.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task DeleteNewsAsync(News news, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns all news settings rows for the given user (read-only query).
        /// </summary>
        /// <param name="userId">User identifier.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>List of news rows for that user (may be empty).</returns>
        Task<List<News>> GetAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the news row with the given id only if it belongs to <paramref name="userId"/>; otherwise <c>null</c> (scoped lookup for authorization).
        /// </summary>
        /// <param name="userId">Owner user id (must match <see cref="News.UserId"/>).</param>
        /// <param name="id">Primary key of the news row.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The news row, or <c>null</c> if not found or not owned by the user.</returns>
        Task<News?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes all news rows for the given user. Returns how many rows were removed.
        /// </summary>
        /// <param name="userId">Owner user id.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>Number of deleted rows.</returns>
        Task<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Inserts a notification log entry and saves changes immediately.
        /// </summary>
        /// <param name="log">The log entry to insert.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default);

        /// <summary>
        /// Looks up a notification log by the same <see cref="NotificationLog.Id"/> as <paramref name="log"/>.
        /// </summary>
        /// <param name="log">Entity whose <c>Id</c> is used for the lookup.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The persisted log if found; otherwise <c>null</c>.</returns>
        Task<NotificationLog?> GetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists all pending changes to the database.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The number of state entries written.</returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
