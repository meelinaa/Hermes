using Hermes.Domain.DTOs;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.Exceptions;
using Hermes.Domain.Interfaces.DBContext;
using Microsoft.EntityFrameworkCore;
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
    public async Task SetUserAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
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
            return null;
        var user = await Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Name == name, cancellationToken)
            .ConfigureAwait(false);
        return user is null ? null : MapToUserScope(user);
    }

    /// <inheritdoc />
    public async Task<UserScope?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;
        var normalized = email.Trim().ToLowerInvariant();
        var user = await Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalized, cancellationToken)
            .ConfigureAwait(false);
        return user is null ? null : MapToUserScope(user);
    }

    /// <inheritdoc />
    public async Task<UserScope?> GetUserByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
            return null;
        var user = await Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return user is null ? null : MapToUserScope(user);
    }

    /// <inheritdoc />
    public async Task<User?> GetUserEntityForAuthenticationByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        return await Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Name == name, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<User?> GetUserEntityForAuthenticationByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;
        var normalized = email.Trim().ToLowerInvariant();
        return await Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == normalized, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        Users.Update(user);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteUserAsync(UserScope user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        User userEntity = MapToUserEntity(user);
        Users.Remove(userEntity);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetNewsAsync(News news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        await News.AddAsync(news, cancellationToken).ConfigureAwait(false);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateNewsAsync(News news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        News.Update(news);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteNewsAsync(News news, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(news);
        News.Remove(news);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<List<News>> GetAllNewsByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await News.AsNoTracking()
            .Where(n => n.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<News?> GetNewsByIdAsync(int userId, int id, CancellationToken cancellationToken = default)
    {
        return await News.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
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
        await NotificationLogs.AddAsync(log, cancellationToken).ConfigureAwait(false);
        await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<NotificationLog?> GetNotificationLogAsync(NotificationLog log, CancellationToken cancellationToken = default)
    {
        return await NotificationLogs.FirstOrDefaultAsync(u => u.Id == log.Id, cancellationToken).ConfigureAwait(false);
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
}
