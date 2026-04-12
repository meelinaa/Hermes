using Hermes.Domain.DTOs;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.Exceptions;
using Hermes.Domain.Interfaces.DBContext;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System.Text.Json;

namespace Hermes.Infrastructure.Data;

/// <summary>
/// EF Core database context for Hermes (MySQL via Pomelo).
/// </summary>
public class HermesDbContext(DbContextOptions<HermesDbContext> options) : DbContext(options), IHermesDbContext
{
    // TODO: Use this in Programm.cs
    //var connectionString = builder.Configuration.GetConnectionString("Hermes")!;
    //builder.Services.AddHermesDbContext(connectionString);

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
            var normalized = user.Email.Trim().ToLowerInvariant();
            user.Email = normalized;
            var exists = await Users.AsNoTracking()
                .AnyAsync(u => u.Email == normalized, cancellationToken)
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

        var user = await Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Name == name, cancellationToken)
            .ConfigureAwait(false);

        return user is null ? throw new UserNotFoundException($"User with name '{name}' was not found.") : MapToUserScope(user);
    }

    /// <inheritdoc />
    public async Task<UserScope?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.", nameof(email));

        var normalized = email.Trim().ToLowerInvariant();
        var user = await Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalized, cancellationToken)
            .ConfigureAwait(false);

        return user is null ? throw new UserNotFoundException($"User with email '{email}' was not found.") : MapToUserScope(user);
    }

    /// <inheritdoc />
    public async Task<UserScope?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id), id, "User id must be greater than zero.");

        var user = await Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return user is null ? throw new UserNotFoundException($"User with id '{id}' was not found.") : MapToUserScope(user);
    }

    /// <inheritdoc />
    public async Task<User?> GetUserEntityForAuthenticationByNameAsync(string name, CancellationToken cancellationToken = default) // This method is used for authentication, so it returns the full User entity (including password hash), not just the UserScope.
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        User? user = await Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Name == name, cancellationToken)
            .ConfigureAwait(false);
        return user is null ? throw new UserNotFoundException() : user;
    }

    /// <inheritdoc />
    public async Task<User?> GetUserEntityForAuthenticationByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;
        var normalized = email.Trim().ToLowerInvariant();
        User? user = await Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalized, cancellationToken)
            .ConfigureAwait(false);
        return user is null ? throw new UserNotFoundException() : user;


    }

    /// <inheritdoc />
    public async Task UpdateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id <= 0)
            throw new ArgumentException("User id must be greater than zero for update.", nameof(user));

        var exists = await Users.AsNoTracking()
            .AnyAsync(u => u.Id == user.Id, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new UserNotFoundException($"User with id {user.Id} was not found.");

        Users.Update(user);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteUserAsync(UserScope user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.UserId <= 0)
            throw new ArgumentException("User id must be greater than zero.", nameof(user));

        var exists = await Users.AsNoTracking()
            .AnyAsync(u => u.Id == user.UserId, cancellationToken)
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

        var existing = await News.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == news.Id, cancellationToken)
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

        var exists = await News.AsNoTracking()
            .AnyAsync(n => n.Id == news.Id && n.UserId == news.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new NewsNotFoundException($"News with id {news.Id} was not found for user {news.UserId}.");

        News.Remove(news);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<List<News>> GetAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
         if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), userId, "User id must be greater than zero.");

        await EnsureUserExistsAsync(userId, cancellationToken).ConfigureAwait(false);

        List<News> news = await News.AsNoTracking()
            .Where(n => n.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return news is null ? throw new NewsNotFoundException() : news;
    }

    /// <inheritdoc />
    public async Task<News?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || id <= 0)
            throw new ArgumentOutOfRangeException();

        News news = await News.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        return news is null ? throw new NewsNotFoundException() : news;

    }

    /// <inheritdoc />
    public async Task<int> DeleteAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), "User id must be greater than zero.");
        return await News.Where(n => n.UserId == userId)
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
            .FirstOrDefaultAsync(u => u.Id == log.Id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        await RefreshTokens.AddAsync(token, cancellationToken).ConfigureAwait(false);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="IHermesDbContext.GetActiveRefreshTokenByHashAsync" />
    public async Task<RefreshToken?> GetActiveRefreshTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(tokenHash))
            return null;
        var utc = DateTime.UtcNow;
        return await RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(
                r => r.TokenHash == tokenHash && r.RevokedAt == null && r.ExpiresAt > utc,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CompleteRefreshRotationAsync(RefreshToken trackedOld, RefreshToken newToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trackedOld);
        ArgumentNullException.ThrowIfNull(newToken);
        trackedOld.RevokedAt = DateTime.UtcNow;
        await RefreshTokens.AddAsync(newToken, cancellationToken).ConfigureAwait(false);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        trackedOld.ReplacedByTokenId = newToken.Id;
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="IHermesDbContext.RevokeRefreshTokenAsync" />
    public async Task RevokeRefreshTokenAsync(RefreshToken trackedToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trackedToken);
        trackedToken.RevokedAt = DateTime.UtcNow;
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="IHermesDbContext.RevokeAllRefreshTokensForUserAsync" />
    public async Task RevokeAllRefreshTokensForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var utc = DateTime.UtcNow;
        var active = await RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null && r.ExpiresAt > utc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var r in active)
            r.RevokedAt = utc;
        if (active.Count > 0)
            await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(e => e.Id);
            entity.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<News>(entity =>
        {
            entity.ToTable("news");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);

            entity.HasOne<User>()
                .WithMany(u => u.News)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Keywords)
                .HasConversion(
                    v => v == null ? null : JsonSerializer.Serialize(v),
                    v => string.IsNullOrEmpty(v) ? null : JsonSerializer.Deserialize<List<string>>(v));

            entity.Property(e => e.Category)
                .HasConversion(
                    v => v == null ? null : JsonSerializer.Serialize(v, HermesJsonOptions.ForEnums),
                    v => string.IsNullOrEmpty(v)
                        ? null
                        : JsonSerializer.Deserialize<List<NewsCategory>>(v, HermesJsonOptions.ForEnums));

            entity.Property(e => e.Languages)
                .HasConversion(
                    v => v == null ? null : JsonSerializer.Serialize(v, HermesJsonOptions.ForEnums),
                    v => string.IsNullOrEmpty(v)
                        ? null
                        : JsonSerializer.Deserialize<List<Language>>(v, HermesJsonOptions.ForEnums));

            entity.Property(e => e.Countries)
                .HasConversion(
                    v => v == null ? null : JsonSerializer.Serialize(v, HermesJsonOptions.ForEnums),
                    v => string.IsNullOrEmpty(v)
                        ? null
                        : JsonSerializer.Deserialize<List<Country>>(v, HermesJsonOptions.ForEnums));

            entity.Property(e => e.SendOnWeekdays)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, HermesJsonOptions.ForEnums),
                    v => JsonSerializer.Deserialize<List<Weekdays>>(v, HermesJsonOptions.ForEnums) ?? new List<Weekdays>());

            entity.Property(e => e.SendAtTimes)
                .HasConversion(
                    v => JsonSerializer.Serialize(v),
                    v => JsonSerializer.Deserialize<List<TimeOnly>>(v) ?? new List<TimeOnly>());
        });

        modelBuilder.Entity<NotificationLog>(entity =>
        {
            entity.ToTable("notification_logs");
            entity.HasKey(e => e.Id);

            entity.HasOne(n => n.User)
                .WithMany(u => u.NotificationLogs)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.Channel).HasConversion<string>();
        });

        // Long-lived sessions: one row per issued refresh token; TokenHash is unique for lookup after client sends plain token.
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasIndex(e => e.UserId);

            entity.HasOne(e => e.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static UserScope MapToUserScope(User user) => new()
    {
        UserId = user.Id,
        Name = user.Name ?? string.Empty,
        Email = user.Email ?? string.Empty
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
        var exists = await Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId, cancellationToken)
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
                // 1452: cannot add/update child row (FK to missing parent)
                if (mysql.Number == 1452)
                    throw new UserNotFoundException("A related record was not found (foreign key constraint).");
                // 1062: duplicate entry (unique index, e.g. email race)
                if (mysql.Number == 1062)
                    throw new EmailAlreadyExistsException("A unique constraint was violated.");
            }

            throw;
        }
    }
}
