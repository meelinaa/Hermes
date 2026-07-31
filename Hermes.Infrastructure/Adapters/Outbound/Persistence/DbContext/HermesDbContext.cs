using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;

/// <summary>
/// EF Core database context for Hermes (MySQL via Pomelo): mappings and coordinated save semantics only.
/// </summary>
/// <remarks>Type-specific persistence lives in <see cref="Hermes.Infrastructure.Adapters.Outbound.Repositories.UserStore"/>, <see cref="Hermes.Infrastructure.Adapters.Outbound.Repositories.NewsStore"/>, and sibling types.</remarks>
public class HermesDbContext(DbContextOptions<HermesDbContext> options) : DbContext(options)
{
    /// <inheritdoc />
    public DbSet<User> Users { get; set; } = null!;

    /// <inheritdoc />
    public DbSet<NewsletterSubscription> NewsletterSubscriptions { get; set; } = null!;

    /// <inheritdoc />
    public DbSet<NotificationLog> NotificationLogs { get; set; } = null!;

    /// <inheritdoc />
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(userEntity => userEntity.Id);
            entity.HasIndex(userEntity => userEntity.Email).IsUnique();
        });

        modelBuilder.Entity<NewsletterSubscription>(entity =>
        {
            entity.ToTable("news");
            entity.HasKey(newsEntity => newsEntity.Id);
            entity.HasIndex(newsEntity => newsEntity.UserId);

            entity.HasOne<User>()
                .WithMany(userEntity => userEntity.NewsletterSubscriptions)
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

            entity.Property(newsEntity => newsEntity.NextDigestSlotUtc);
            entity.HasIndex(newsEntity => newsEntity.NextDigestSlotUtc);
            entity.Property(newsEntity => newsEntity.IsEnabled).HasDefaultValue(true);
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
            entity.Property(notificationLog => notificationLog.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(notificationLog => notificationLog.Channel).HasConversion<string>().HasMaxLength(32);

            entity.HasIndex(
                    notificationLog => new { notificationLog.UserId, notificationLog.NewsId, notificationLog.Channel, notificationLog.Status, notificationLog.SentAt })
                .HasDatabaseName("IX_notification_logs_dedupe_window");
            entity.HasIndex(
                    notificationLog => new { notificationLog.Status, notificationLog.NextRetryAt, notificationLog.Id })
                .HasDatabaseName("IX_notification_logs_pending_retry");
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

    /// <inheritdoc />
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            throw MySqlDbUpdateExceptionTranslator.Transform(ex);
        }
    }
}
