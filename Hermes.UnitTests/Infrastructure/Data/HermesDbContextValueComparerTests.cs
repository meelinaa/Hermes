using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hermes.UnitTests.Infrastructure.Data;

public sealed class HermesDbContextValueComparerTests
{
    private static HermesDbContext CreateInMemoryContext()
    {
        DbContextOptions<HermesDbContext> options = new DbContextOptionsBuilder<HermesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new HermesDbContext(options);
    }

    [Fact]
    public async Task ValueComparer_Should_DetectEqualSequences_As_Unchanged()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        var subscription = NewsletterSubscription.CreateForUser(new UserId(1));
        subscription.UpdateFilters(
            ["dotnet", "csharp"],
            [NewsCategory.Technology],
            [Language.German],
            [Country.Germany]);
        subscription.SetSchedule(
            [Weekdays.Monday, Weekdays.Friday],
            [new TimeOnly(8, 0), new TimeOnly(18, 30)]);

        ctx.NewsletterSubscriptions.Add(subscription);
        await ctx.SaveChangesAsync();

        // Act - reassign with new List instance containing same elements
        subscription.UpdateFilters(
            ["dotnet", "csharp"],
            [NewsCategory.Technology],
            [Language.German],
            [Country.Germany]);
        subscription.SetSchedule(
            [Weekdays.Monday, Weekdays.Friday],
            [new TimeOnly(8, 0), new TimeOnly(18, 30)]);

        // Assert - EF Core change tracker detects values are identical through ValueComparer
        var entry = ctx.Entry(subscription);
        Assert.False(entry.Property(e => e.Keywords).IsModified);
        Assert.False(entry.Property(e => e.Category).IsModified);
        Assert.False(entry.Property(e => e.Languages).IsModified);
        Assert.False(entry.Property(e => e.Countries).IsModified);
        Assert.False(entry.Property(e => e.SendOnWeekdays).IsModified);
        Assert.False(entry.Property(e => e.SendAtTimes).IsModified);
    }

    [Fact]
    public async Task ValueComparer_Should_DetectModifiedSequences_As_Modified()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        var subscription = NewsletterSubscription.CreateForUser(new UserId(1));
        subscription.UpdateFilters(
            ["ai"],
            [NewsCategory.Science],
            [Language.English],
            [Country.USA]);
        subscription.SetSchedule(
            [Weekdays.Monday],
            [new TimeOnly(9, 0)]);

        ctx.NewsletterSubscriptions.Add(subscription);
        await ctx.SaveChangesAsync();

        // Act - update with different elements
        subscription.UpdateFilters(
            ["ai", "machine-learning"],
            [NewsCategory.Science, NewsCategory.Technology],
            [Language.English, Language.German],
            [Country.USA, Country.Germany]);
        subscription.SetSchedule(
            [Weekdays.Monday, Weekdays.Wednesday],
            [new TimeOnly(9, 0), new TimeOnly(17, 0)]);

        // Assert - EF Core change tracker detects changes through ValueComparer
        var entry = ctx.Entry(subscription);
        Assert.True(entry.Property(e => e.Keywords).IsModified);
        Assert.True(entry.Property(e => e.Category).IsModified);
        Assert.True(entry.Property(e => e.Languages).IsModified);
        Assert.True(entry.Property(e => e.Countries).IsModified);
        Assert.True(entry.Property(e => e.SendOnWeekdays).IsModified);
        Assert.True(entry.Property(e => e.SendAtTimes).IsModified);
    }

    [Fact]
    public async Task ValueComparer_Should_Handle_Null_Collections()
    {
        // Arrange
        await using HermesDbContext ctx = CreateInMemoryContext();
        var subscription = NewsletterSubscription.CreateForUser(new UserId(1));
        subscription.UpdateFilters(null, null, null, null);
        subscription.SetSchedule(
            [Weekdays.Monday],
            [new TimeOnly(9, 0)]);

        ctx.NewsletterSubscriptions.Add(subscription);
        await ctx.SaveChangesAsync();

        // Act - keep null
        subscription.UpdateFilters(null, null, null, null);

        // Assert
        var entry = ctx.Entry(subscription);
        Assert.False(entry.Property(e => e.Keywords).IsModified);
        Assert.False(entry.Property(e => e.Category).IsModified);
        Assert.False(entry.Property(e => e.Languages).IsModified);
        Assert.False(entry.Property(e => e.Countries).IsModified);
    }
}
