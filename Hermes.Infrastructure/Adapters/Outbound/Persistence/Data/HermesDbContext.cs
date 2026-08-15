using System.Text.Json;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.Events;
using Hermes.Domain.ValueObjects;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Mappers;
using Microsoft.EntityFrameworkCore;
using Hermes.Application.Ports.Outbound;

namespace Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;

/// <summary>
/// EF Core database context for Hermes (MySQL via Pomelo): mappings and coordinated save semantics only.
/// </summary>
/// <remarks>Type-specific persistence lives in <see cref="Hermes.Infrastructure.Adapters.Outbound.Repositories.UserStore"/>, <see cref="Hermes.Infrastructure.Adapters.Outbound.Repositories.NewsStore"/>, and sibling types.</remarks>
public class HermesDbContext(DbContextOptions<HermesDbContext> options, IDomainEventDispatcher? dispatcher = null) : DbContext(options)
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
            entity.Property(userEntity => userEntity.Id)
                .HasConversion(id => id.Value, val => new UserId(val))
                .ValueGeneratedOnAdd();
            entity.Property(userEntity => userEntity.Email).HasConversion(e => e.Value, val => Email.Parse(val));
            entity.HasIndex(userEntity => userEntity.Email).IsUnique();

            entity.Navigation(e => e.NewsletterSubscriptions).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(e => e.NotificationLogs).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(e => e.RefreshTokens).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<NewsletterSubscription>(entity =>
        {
            entity.ToTable("news");
            entity.HasKey(newsEntity => newsEntity.Id);
            entity.Property(newsEntity => newsEntity.Id)
                .HasConversion(id => id.Value, val => new NewsletterId(val))
                .ValueGeneratedOnAdd();
            entity.Property(newsEntity => newsEntity.UserId).HasConversion(id => id.Value, val => new UserId(val));
            entity.HasIndex(newsEntity => newsEntity.UserId);

            entity.HasOne<User>()
                .WithMany(userEntity => userEntity.NewsletterSubscriptions)
                .HasForeignKey(newsEntity => newsEntity.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(newsEntity => newsEntity.Keywords)
                .HasConversion(
                    keywordValues => keywordValues == null ? null : JsonSerializer.Serialize(keywordValues, (JsonSerializerOptions?)null),
                    serializedKeywords => string.IsNullOrEmpty(serializedKeywords) ? null : JsonSerializer.Deserialize<IReadOnlyList<string>>(serializedKeywords, (JsonSerializerOptions?)null));

            entity.Property(newsEntity => newsEntity.Category)
                .HasConversion(
                    categoryValues => categoryValues == null ? null : JsonSerializer.Serialize(categoryValues, HermesJsonOptions._forEnums),
                    serializedCategories => string.IsNullOrEmpty(serializedCategories)
                        ? null
                        : JsonSerializer.Deserialize<IReadOnlyList<NewsCategory>>(serializedCategories, HermesJsonOptions._forEnums));

            entity.Property(newsEntity => newsEntity.Languages)
                .HasConversion(
                    languageValues => languageValues == null ? null : JsonSerializer.Serialize(languageValues, HermesJsonOptions._forEnums),
                    serializedLanguages => string.IsNullOrEmpty(serializedLanguages)
                        ? null
                        : JsonSerializer.Deserialize<IReadOnlyList<Language>>(serializedLanguages, HermesJsonOptions._forEnums));

            entity.Property(newsEntity => newsEntity.Countries)
                .HasConversion(
                    countryValues => countryValues == null ? null : JsonSerializer.Serialize(countryValues, HermesJsonOptions._forEnums),
                    serializedCountries => string.IsNullOrEmpty(serializedCountries)
                        ? null
                        : JsonSerializer.Deserialize<IReadOnlyList<Country>>(serializedCountries, HermesJsonOptions._forEnums));

            entity.Property(newsEntity => newsEntity.SendOnWeekdays)
                .HasConversion(
                    weekdayValues => JsonSerializer.Serialize(weekdayValues ?? new List<Weekdays>(), HermesJsonOptions._forEnums),
                    serializedWeekdays => string.IsNullOrWhiteSpace(serializedWeekdays)
                        ? Array.Empty<Weekdays>()
                        : JsonSerializer.Deserialize<IReadOnlyList<Weekdays>>(serializedWeekdays, HermesJsonOptions._forEnums) ?? Array.Empty<Weekdays>());

            entity.Property(newsEntity => newsEntity.SendAtTimes)
                .HasConversion(
                    sendAtTimes => JsonSerializer.Serialize(sendAtTimes ?? new List<TimeOnly>(), (JsonSerializerOptions?)null),
                    serializedSendAtTimes => string.IsNullOrWhiteSpace(serializedSendAtTimes)
                        ? Array.Empty<TimeOnly>()
                        : JsonSerializer.Deserialize<IReadOnlyList<TimeOnly>>(serializedSendAtTimes, (JsonSerializerOptions?)null) ?? Array.Empty<TimeOnly>());

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

            entity.Property(notificationLog => notificationLog.UserId).HasConversion(id => id.Value, val => new UserId(val));
            entity.Property(notificationLog => notificationLog.NewsId).HasConversion(id => id.HasValue ? id.Value.Value : (int?)null, val => val.HasValue ? new NewsletterId(val.Value) : (NewsletterId?)null);
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

            entity.Property(refreshToken => refreshToken.UserId).HasConversion(id => id.Value, val => new UserId(val));

            entity.HasOne(refreshToken => refreshToken.ReplacedByToken)
                .WithMany()
                .HasForeignKey(refreshToken => refreshToken.ReplacedByTokenId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    /// <summary>
    /// Persists all pending entity changes to the database and dispatches domain events upon successful commit.
    /// Saves the database changes first so that auto-increment identity keys are assigned to new entities before events are dispatched to handlers or background queues.
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess">Indicates whether all changes should be accepted if the operation succeeds.</param>
    /// <param name="cancellationToken">A token to observe while waiting for the save operation to complete.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var entriesWithEvents = ChangeTracker.Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => new { Entry = e, e.Entity, Events = e.Entity.DomainEvents.ToList() })
            .ToList();

        int result;
        try
        {
            result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            var error = MySqlDbUpdateExceptionMapper.MapToError(ex);
            if (error != null)
            {
                if (error.Message.Contains("unique constraint"))
                    throw new Hermes.Domain.Exceptions.EmailAlreadyExistsException(error.Message);
                if (error.Message.Contains("foreign key constraint"))
                    throw new Hermes.Domain.Exceptions.UserNotFoundException(error.Message);
                
                throw new Hermes.Domain.Exceptions.DomainValidationException(error.Message);
            }
            throw;
        }

        if (dispatcher is not null)
        {
            foreach (var item in entriesWithEvents)
            {
                item.Entity.ClearDomainEvents();
                foreach (var domainEvent in item.Events)
                {
                    var eventToDispatch = domainEvent switch
                    {
                        UserRegisteredEvent ure when ure.UserId.Value <= 0 && item.Entity is User u
                            => ure with { UserId = u.Id },
                        UserEmailChangedEvent uec when uec.UserId.Value <= 0 && item.Entity is User u
                            => uec with { UserId = u.Id },
                        _ => domainEvent
                    };

                    await dispatcher.DispatchAsync(eventToDispatch, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return result;
    }
}
