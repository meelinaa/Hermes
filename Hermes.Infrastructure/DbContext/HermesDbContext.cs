using Hermes.Application.Models.News;
using Hermes.Domain.DTOs;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.Exceptions;
using Hermes.Application.Ports;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Text.Json;

namespace Hermes.Infrastructure.Data;

/// <summary>
/// EF Core database context for Hermes (MySQL via Pomelo).
/// </summary>
public class HermesDbContext(DbContextOptions<HermesDbContext> options) : DbContext(options), IHermesDataStore
{
    /// <inheritdoc />
    public DbSet<User> Users { get; set; } = null!;

    /// <inheritdoc />
    public DbSet<News> News { get; set; } = null!;

    /// <inheritdoc />
    public DbSet<NotificationLog> NotificationLogs { get; set; } = null!;

    /// <inheritdoc />
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    /// <inheritdoc />
    public async Task SetUserAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id != 0)
            throw new ArgumentException("New users must have id 0 before insert.", nameof(user));

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            string normalized = user.Email.Trim().ToLowerInvariant();
            user.Email = normalized;
            bool exists = await Users.AsNoTracking()
                .AnyAsync(userEntity => userEntity.Email == normalized, cancellationToken)
                .ConfigureAwait(false);
            if (exists)
                throw new EmailAlreadyExistsException();
        }

        await Users.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<UserScope?> GetUserByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        User? user = await Users.AsNoTracking()
            .FirstOrDefaultAsync(userEntity => userEntity.Name == name, cancellationToken)
            .ConfigureAwait(false);

        return user is null ? throw new UserNotFoundException($"User with name '{name}' was not found.") : MapToUserScope(user);
    }

    /// <inheritdoc />
    public async Task<UserScope?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.", nameof(email));

        string normalized = email.Trim().ToLowerInvariant();
        User? user = await Users.AsNoTracking()
            .FirstOrDefaultAsync(userEntity => userEntity.Email != null && userEntity.Email == normalized, cancellationToken)
            .ConfigureAwait(false);

        return user is null ? throw new UserNotFoundException($"User with email '{email}' was not found.") : MapToUserScope(user);
    }

    /// <inheritdoc />
    public async Task<UserScope?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), id, "User id must be greater than zero.");

        User? user = await Users.AsNoTracking()
            .FirstOrDefaultAsync(userEntity => userEntity.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return user is null ? throw new UserNotFoundException($"User with id '{id}' was not found.") : MapToUserScope(user);
    }

    /// <inheritdoc />
    public async Task<User?> GetUserEntityForAuthenticationByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        User? user = await Users.AsNoTracking()
            .FirstOrDefaultAsync(userEntity => userEntity.Name == name, cancellationToken)
            .ConfigureAwait(false);
        return user is null ? throw new UserNotFoundException() : user;
    }

    /// <inheritdoc />
    public async Task<User?> GetUserEntityForAuthenticationByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        string normalized = email.Trim().ToLowerInvariant();

        User? user = await Users.AsNoTracking()
            .FirstOrDefaultAsync(userEntity => userEntity.Email == normalized, cancellationToken)
            .ConfigureAwait(false);

        return user ?? throw new UserNotFoundException();
    }

    /// <inheritdoc />
    public async Task<User?> GetUserEntityForAuthenticationByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), id, "User id must be greater than zero.");

        User? user = await Users
            .AsNoTracking()
            .FirstOrDefaultAsync(userEntity => userEntity.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return user ?? throw new UserNotFoundException();
    }

    /// <inheritdoc />
    public async Task<User?> GetUserEntityByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;
        return await Users.AsNoTracking()
            .FirstOrDefaultAsync(userEntity => userEntity.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id <= 0)
            throw new ArgumentException("User id must be greater than zero for update.", nameof(user));

        User? entity = await Users.FirstOrDefaultAsync(userEntity => userEntity.Id == user.Id, cancellationToken).ConfigureAwait(false);
        if (entity is null)
            throw new UserNotFoundException($"User with id {user.Id} was not found.");

        entity.Name = user.Name;

        if (entity.Email != user.Email)
            entity.IsEmailVerified = false;
        entity.Email = user.Email;

        if (!string.IsNullOrWhiteSpace(user.PasswordHash))
            entity.PasswordHash = user.PasswordHash;

        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteUserAsync(UserScope user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.UserId <= 0)
            throw new ArgumentException("User id must be greater than zero.", nameof(user));

        bool exists = await Users.AsNoTracking()
            .AnyAsync(userEntity => userEntity.Id == user.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new UserNotFoundException($"User with id {user.UserId} was not found.");

        User userEntity = MapToUserEntity(user);
        Users.Remove(userEntity);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetNewsAsync(News news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        if (news.Id != 0)
            throw new ArgumentException("Insert requires news id 0; use update for an existing row.", nameof(news));

        await EnsureUserExistsAsync(news.UserId, cancellationToken).ConfigureAwait(false);
        await News.AddAsync(news, cancellationToken).ConfigureAwait(false);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateNewsAsync(News news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        if (news.UserId <= 0)
            throw new ArgumentException("News.UserId must be greater than zero.", nameof(news));
        if (news.Id <= 0)
            throw new NewsNotFoundException("A valid news id is required for update.");

        News? existing = await News.AsNoTracking()
            .FirstOrDefaultAsync(newsEntity => newsEntity.Id == news.Id, cancellationToken)
            .ConfigureAwait(false);
        if (existing is null)
            throw new NewsNotFoundException($"News with id {news.Id} was not found.");
        if (existing.UserId != news.UserId)
            throw new NewsAccessDeniedException("This news entry belongs to another user.");

        await EnsureUserExistsAsync(news.UserId, cancellationToken).ConfigureAwait(false);
        News.Update(news);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteNewsAsync(News news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        if (news.Id <= 0)
            throw new ArgumentException("News id must be greater than zero.", nameof(news));
        if (news.UserId <= 0)
            throw new ArgumentException("News.UserId must be greater than zero.", nameof(news));

        bool exists = await News.AsNoTracking()
            .AnyAsync(newsEntity => newsEntity.Id == news.Id && newsEntity.UserId == news.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new NewsNotFoundException($"News with id {news.Id} was not found for user {news.UserId}.");

        News.Remove(news);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<NewsListResult> GetNewsListAsync(NewsListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.UserId <= 0)
            throw new ArgumentOutOfRangeException(nameof(query.UserId), query.UserId, "User id must be greater than zero.");
        if (query.Page < 1)
            throw new ArgumentOutOfRangeException(nameof(query.Page), query.Page, "Page must be at least 1.");
        if (query.PageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(query.PageSize), query.PageSize, "Page size must be at least 1.");

        await EnsureUserExistsAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        IQueryable<News> filtered = News.AsNoTracking().Where(n => n.UserId == query.UserId);

        if (query.Category is NewsCategory category)
            filtered = filtered.Where(n => n.Category != null && n.Category.Contains(category));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search.Trim();
            if (term.Length > 200)
                term = term[..200];

            filtered = filtered.Where(n => n.Keywords != null && n.Keywords.Any(k => k.Contains(term)));
        }

        if (query.AfterId is int after)
        {
            IQueryable<News> cursorQuery = filtered.OrderBy(n => n.Id).Where(n => n.Id > after);
            List<News> window = await cursorQuery
                .Take(query.PageSize + 1)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            bool hasNext = window.Count > query.PageSize;
            if (hasNext)
                window.RemoveAt(window.Count - 1);
            int? nextAfter = hasNext && window.Count > 0 ? window[^1].Id : null;
            return new NewsListResult(window, 1, query.PageSize, null, null, hasNext, nextAfter);
        }

        IQueryable<News> ordered = query.SortDescending
            ? filtered.OrderByDescending(n => n.Id)
            : filtered.OrderBy(n => n.Id);

        int total = await ordered.CountAsync(cancellationToken).ConfigureAwait(false);
        int skip = (query.Page - 1) * query.PageSize;
        List<News> items = await ordered
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        int totalPages = query.PageSize > 0 ? (int)Math.Ceiling(total / (double)query.PageSize) : 0;
        bool hasNextPage = skip + items.Count < total;
        return new NewsListResult(items, query.Page, query.PageSize, total, totalPages, hasNextPage, null);
    }

    /// <inheritdoc />
    public async Task<List<NewsScheduleRow>> GetNewsScheduleRowsAsync(CancellationToken cancellationToken = default)
    {
        return await News.AsNoTracking()
            .Select(newsEntity => new NewsScheduleRow(newsEntity.Id, newsEntity.UserId, newsEntity.SendOnWeekdays, newsEntity.SendAtTimes))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<List<(int NewsId, int UserId)>> GetDueNewsScheduleForSlotAsync(
        Weekdays weekday,
        int hour,
        int minute,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(hour, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(hour, 23);
        ArgumentOutOfRangeException.ThrowIfLessThan(minute, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minute, 59);

        // Labels must match JSON string elements produced by the same serializers as the EF value converters.
        string weekdayLabel = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(weekday, HermesJsonOptions.ForEnums))!;
        string timeLabel = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(new TimeOnly(hour, minute)))!;
        List<DueNewsScheduleSlotRow> rows = await Database
            .SqlQueryRaw<DueNewsScheduleSlotRow>(
                """
                SELECT n.Id AS Id, n.UserId AS UserId
                FROM news n
                WHERE n.Id > 0 AND n.UserId > 0
                  AND JSON_SEARCH(
                    IF(
                      n.SendOnWeekdays IS NOT NULL
                      AND CHAR_LENGTH(TRIM(n.SendOnWeekdays)) > 0
                      AND JSON_VALID(n.SendOnWeekdays),
                      n.SendOnWeekdays,
                      '[]'),
                    'one',
                    {0},
                    NULL,
                    '$[*]') IS NOT NULL
                  AND JSON_SEARCH(
                    IF(
                      n.SendAtTimes IS NOT NULL
                      AND CHAR_LENGTH(TRIM(n.SendAtTimes)) > 0
                      AND JSON_VALID(n.SendAtTimes),
                      n.SendAtTimes,
                      '[]'),
                    'one',
                    {1},
                    NULL,
                    '$[*]') IS NOT NULL
                ORDER BY n.Id
                """,
                weekdayLabel,
                timeLabel)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<(int NewsId, int UserId)> result = new(rows.Count);
        foreach (DueNewsScheduleSlotRow row in rows)
            result.Add((row.Id, row.UserId));

        return result;
    }

    /// <inheritdoc />
    public async Task<News?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), userId, "User id must be greater than zero.");
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), id, "News id must be greater than zero.");

        News? news = await News.AsNoTracking()
            .FirstOrDefaultAsync(newsEntity => newsEntity.Id == id && newsEntity.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        return news is null ? throw new NewsNotFoundException() : news;

    }

    /// <inheritdoc />
    public async Task<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User id must be greater than zero.");
        return await News.Where(newsEntity => newsEntity.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (log.Id != 0)
            throw new ArgumentException("New notification logs must have id 0 before insert.", nameof(log));

        await EnsureUserExistsAsync(log.UserId, cancellationToken).ConfigureAwait(false);
        await NotificationLogs.AddAsync(log, cancellationToken).ConfigureAwait(false);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<NotificationLog?> GetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (log.Id <= 0)
            return null;

        return await NotificationLogs.AsNoTracking()
            .FirstOrDefaultAsync(notificationLog => notificationLog.Id == log.Id, cancellationToken)
            .ConfigureAwait(false);
    }

   

    /// <inheritdoc cref="IHermesDataStore.GetActiveRefreshTokenByHashAsync" />
    public async Task<RefreshToken?> GetActiveRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(tokenHash))
            return null;
        DateTime utc = DateTime.UtcNow;
        return await RefreshTokens
            .Include(refreshToken => refreshToken.User)
            .FirstOrDefaultAsync(
                refreshToken => refreshToken.TokenHash == tokenHash && refreshToken.RevokedAt == null && refreshToken.ExpiresAt > utc,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(tokenHash))
            return null;
        return await RefreshTokens
            .Include(refreshToken => refreshToken.User)
            .FirstOrDefaultAsync(
                refreshToken => refreshToken.TokenHash == tokenHash,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CompleteRefreshRotationAsync(RefreshToken trackedOld, RefreshToken newToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trackedOld);
        ArgumentNullException.ThrowIfNull(newToken);

        if (Database.IsRelational())
        {
            using var transaction = await Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await RefreshTokens.AddAsync(newToken, cancellationToken).ConfigureAwait(false);
                trackedOld.RevokedAt = DateTime.UtcNow;
                trackedOld.ReplacedByToken = newToken;
                await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
        else
        {
            await RefreshTokens.AddAsync(newToken, cancellationToken).ConfigureAwait(false);
            trackedOld.RevokedAt = DateTime.UtcNow;
            trackedOld.ReplacedByToken = newToken;
            await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc cref="IHermesDataStore.RevokeRefreshTokenAsync" />
    public async Task RevokeRefreshTokenAsync(RefreshToken trackedToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trackedToken);
        trackedToken.RevokedAt = DateTime.UtcNow;
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        await RefreshTokens.AddAsync(token, cancellationToken).ConfigureAwait(false);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="IHermesDataStore.RevokeAllRefreshTokensForUserAsync" />
    public async Task RevokeAllRefreshTokensForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        DateTime utc = DateTime.UtcNow;
        List<RefreshToken> active = await RefreshTokens
            .Where(refreshToken => refreshToken.UserId == userId && refreshToken.RevokedAt == null && refreshToken.ExpiresAt > utc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (RefreshToken activeToken in active)
            activeToken.RevokedAt = utc;
        if (active.Count > 0)
            await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RevokeTokenFamilyAsync(RefreshToken compromisedToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(compromisedToken);
        DateTime utc = DateTime.UtcNow;

        List<RefreshToken> userTokens = await RefreshTokens
            .Where(t => t.UserId == compromisedToken.UserId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var queue = new Queue<RefreshToken>();
        queue.Enqueue(compromisedToken);

        bool changesMade = false;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.RevokedAt == null)
            {
                current.RevokedAt = utc;
                changesMade = true;
            }

            // Rotation chain: each revoked row's FK points forward to its replacement (successor), not backward.
            if (current.ReplacedByTokenId is { } successorId)
            {
                RefreshToken? successor = userTokens.FirstOrDefault(t => t.Id == successorId);
                if (successor != null)
                    queue.Enqueue(successor);
            }
        }

        if (changesMade)
            await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsSentNotificationInWindowAsync(
        int userId,
        int newsId,
        DateTime windowStartUtc,
        DateTime windowEndUtc,
        CancellationToken cancellationToken = default)
    {
        return await NotificationLogs.AsNoTracking()
            .AnyAsync(
                notificationLog => notificationLog.UserId == userId
                                   && notificationLog.NewsId == newsId
                                   && notificationLog.Channel == DeliveryChannel.Email
                                   && notificationLog.Status == NotificationStatus.Sent
                                   && notificationLog.SentAt >= windowStartUtc
                                   && notificationLog.SentAt < windowEndUtc,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetUserEmailVerificationChallengeAsync(
        int userId,
        string verificationCode,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), userId, "User id must be greater than zero.");
        if (string.IsNullOrWhiteSpace(verificationCode))
            throw new ArgumentException("Verification code is required.", nameof(verificationCode));

        DateTime expires = DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc);
        User? user = await Users.FirstOrDefaultAsync(userEntity => userEntity.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
            throw new UserNotFoundException($"User with id {userId} was not found.");

        user.TwoFactorCode = verificationCode.Trim();
        user.TwoFactorExpiry = expires;
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CompleteUserEmailVerificationAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), userId, "User id must be greater than zero.");

        User? user = await Users.FirstOrDefaultAsync(userEntity => userEntity.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
            throw new UserNotFoundException($"User with id {userId} was not found.");

        user.IsEmailVerified = true;
        user.TwoFactorCode = null;
        user.TwoFactorExpiry = null;
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(userEntity => userEntity.Id);
            entity.HasIndex(userEntity => userEntity.Email).IsUnique();
        });

        modelBuilder.Entity<News>(entity =>
        {
            entity.ToTable("news");
            entity.HasKey(newsEntity => newsEntity.Id);
            entity.HasIndex(newsEntity => newsEntity.UserId);

            entity.HasOne<User>()
                .WithMany(userEntity => userEntity.News)
                .HasForeignKey(newsEntity => newsEntity.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(newsEntity => newsEntity.Keywords)
                .HasConversion(
                    keywordValues => keywordValues == null ? null : JsonSerializer.Serialize(keywordValues, (JsonSerializerOptions?)null),
                    serializedKeywords => string.IsNullOrEmpty(serializedKeywords) ? null : JsonSerializer.Deserialize<List<string>>(serializedKeywords, (JsonSerializerOptions?)null));

            entity.Property(newsEntity => newsEntity.Category)
                .HasConversion(
                    categoryValues => categoryValues == null ? null : JsonSerializer.Serialize(categoryValues, HermesJsonOptions.ForEnums),
                    serializedCategories => string.IsNullOrEmpty(serializedCategories)
                        ? null
                        : JsonSerializer.Deserialize<List<NewsCategory>>(serializedCategories, HermesJsonOptions.ForEnums));

            entity.Property(newsEntity => newsEntity.Languages)
                .HasConversion(
                    languageValues => languageValues == null ? null : JsonSerializer.Serialize(languageValues, HermesJsonOptions.ForEnums),
                    serializedLanguages => string.IsNullOrEmpty(serializedLanguages)
                        ? null
                        : JsonSerializer.Deserialize<List<Language>>(serializedLanguages, HermesJsonOptions.ForEnums));

            entity.Property(newsEntity => newsEntity.Countries)
                .HasConversion(
                    countryValues => countryValues == null ? null : JsonSerializer.Serialize(countryValues, HermesJsonOptions.ForEnums),
                    serializedCountries => string.IsNullOrEmpty(serializedCountries)
                        ? null
                        : JsonSerializer.Deserialize<List<Country>>(serializedCountries, HermesJsonOptions.ForEnums));

            entity.Property(newsEntity => newsEntity.SendOnWeekdays)
                .HasConversion(
                    weekdayValues => JsonSerializer.Serialize(weekdayValues ?? new List<Weekdays>(), HermesJsonOptions.ForEnums),
                    serializedWeekdays => string.IsNullOrWhiteSpace(serializedWeekdays)
                        ? new List<Weekdays>()
                        : JsonSerializer.Deserialize<List<Weekdays>>(serializedWeekdays, HermesJsonOptions.ForEnums) ?? new List<Weekdays>());

            entity.Property(newsEntity => newsEntity.SendAtTimes)
                .HasConversion(
                    sendAtTimes => JsonSerializer.Serialize(sendAtTimes ?? new List<TimeOnly>(), (JsonSerializerOptions?)null),
                    serializedSendAtTimes => string.IsNullOrWhiteSpace(serializedSendAtTimes)
                        ? new List<TimeOnly>()
                        : JsonSerializer.Deserialize<List<TimeOnly>>(serializedSendAtTimes, (JsonSerializerOptions?)null) ?? new List<TimeOnly>());
        });

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.ToTable("notification_logs");
            entity.HasKey(notificationLog => notificationLog.Id);

            entity.HasOne<User>()
                .WithMany(userEntity => userEntity.NotificationLogs)
                .HasForeignKey(notificationLog => notificationLog.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(notificationLog => notificationLog.NewsId);
            entity.Property(notificationLog => notificationLog.Status).HasConversion<string>();
            entity.Property(notificationLog => notificationLog.Channel).HasConversion<string>();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(refreshToken => refreshToken.Id);
            entity.HasIndex(refreshToken => refreshToken.TokenHash).IsUnique();
            entity.HasIndex(refreshToken => refreshToken.UserId);

            entity.HasOne(refreshToken => refreshToken.User)
                .WithMany(userEntity => userEntity.RefreshTokens)
                .HasForeignKey(refreshToken => refreshToken.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(refreshToken => refreshToken.ReplacedByToken)
                .WithMany()
                .HasForeignKey(refreshToken => refreshToken.ReplacedByTokenId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static UserScope MapToUserScope(User user) => new()
    {
        UserId = user.Id,
        Name = user.Name ?? string.Empty,
        Email = user.Email ?? string.Empty,
        IsEmailVerified = user.IsEmailVerified
    };

    private static User MapToUserEntity(UserScope scope) => new()
    {
        Id = scope.UserId,
        Name = scope.Name,
        Email = scope.Email
    };

    private async Task EnsureUserExistsAsync(int userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
            throw new UserNotFoundException($"No user with id {userId} exists.");
        bool exists = await Users.AsNoTracking()
            .AnyAsync(userEntity => userEntity.Id == userId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new UserNotFoundException($"No user with id {userId} exists.");
    }

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            if (ex.InnerException is MySqlException mysql)
            {
                if (mysql.Number == 1452)
                    throw new UserNotFoundException("A related record was not found (foreign key constraint).");
                if (mysql.Number == 1062)
                    throw new EmailAlreadyExistsException("A unique constraint was violated.");
            }

            throw;
        }
    }

    /// <summary>Materialization type for <see cref="GetDueNewsScheduleForSlotAsync"/> raw SQL.</summary>
    private sealed class DueNewsScheduleSlotRow
    {
        public int Id { get; set; }

        public int UserId { get; set; }
    }
}
