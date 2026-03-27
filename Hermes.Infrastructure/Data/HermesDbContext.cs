using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Infrastructure.Data.Interfaces;
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
            entity.HasIndex(e => e.UserId).IsUnique();

            entity.HasOne(n => n.User)
                .WithOne(u => u.NewsSettings)
                .HasForeignKey<News>(n => n.UserId)
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
}
