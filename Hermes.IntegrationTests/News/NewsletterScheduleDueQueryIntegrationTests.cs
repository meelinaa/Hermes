using Hermes.Application.Ports;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.IntegrationTests.News;

/// <summary>
/// Integration tests for querying due newsletter schedules.
/// </summary>
[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class NewsletterScheduleDueQueryIntegrationTests(MySqlApiFixture fixture)
{
    /// <summary>
    /// Verifies that GetDueNewsScheduleForSlotAsync returns only subscription rows that match the target weekday and time.
    /// </summary>
    [Fact]
    public async Task GetDueNewsScheduleForSlotAsync_returns_only_rows_matching_weekday_and_time()
    {
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        IUserRepository users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        INewsletterSubscriptionRepository newsStore = scope.ServiceProvider.GetRequiredService<INewsletterSubscriptionRepository>();

        User user = new()
        {
            Id = 0,
            Name = "schedule-due-test",
            Email = $"sched-due-{Guid.NewGuid():N}@test.local",
            PasswordHash = "hash",
            IsEmailVerified = true,
        };
        await users.SetUserAsync(user, CancellationToken.None);
        int userId = user.Id;

        NewsletterSubscription mondaySlot = NewsletterSubscription.CreateForUser(userId);
        mondaySlot.AssignDigestSchedule(Hermes.Domain.ValueObjects.ScheduleWindow.EnsureForDigestScheduling([Weekdays.Monday], [new TimeOnly(9, 30)]));
        await newsStore.SetNewsAsync(mondaySlot, CancellationToken.None);

        NewsletterSubscription wrongWeekday = NewsletterSubscription.CreateForUser(userId);
        wrongWeekday.AssignDigestSchedule(Hermes.Domain.ValueObjects.ScheduleWindow.EnsureForDigestScheduling([Weekdays.Tuesday], [new TimeOnly(9, 30)]));
        await newsStore.SetNewsAsync(wrongWeekday, CancellationToken.None);

        NewsletterSubscription wrongTime = NewsletterSubscription.CreateForUser(userId);
        wrongTime.AssignDigestSchedule(Hermes.Domain.ValueObjects.ScheduleWindow.EnsureForDigestScheduling([Weekdays.Monday], [new TimeOnly(14, 0)]));
        await newsStore.SetNewsAsync(wrongTime, CancellationToken.None);

        // Direct INewsletterSubscriptionRepository inserts leave NextDigestSlotUtc null, so matching uses the JSON_SEARCH path only;
        // slot UTC bounds still apply to materialized rows and must be supplied.
        DateTime slotStartUtc = new(2026, 5, 4, 7, 0, 0, DateTimeKind.Utc);
        DateTime slotEndUtc = slotStartUtc.AddMinutes(1);

        List<(int NewsId, int UserId)> due = await newsStore.GetDueNewsScheduleForSlotAsync(
            Weekdays.Monday,
            9,
            30,
            slotStartUtc,
            slotEndUtc,
            CancellationToken.None);

        Assert.Contains((mondaySlot.Id, userId), due);
        Assert.DoesNotContain((wrongWeekday.Id, userId), due);
        Assert.DoesNotContain((wrongTime.Id, userId), due);
    }

    /// <summary>
    /// Verifies that GetDueNewsScheduleForSlotAsync excludes subscriptions where IsEnabled is false.
    /// </summary>
    [Fact]
    public async Task GetDueNewsScheduleForSlotAsync_excludes_rows_with_IsEnabled_false()
    {
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        IUserRepository users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        INewsletterSubscriptionRepository newsStore = scope.ServiceProvider.GetRequiredService<INewsletterSubscriptionRepository>();

        User user = new()
        {
            Id = 0,
            Name = "schedule-disabled-test",
            Email = $"sched-off-{Guid.NewGuid():N}@test.local",
            PasswordHash = "hash",
            IsEmailVerified = true,
        };
        await users.SetUserAsync(user, CancellationToken.None);
        int userId = user.Id;

        NewsletterSubscription enabledRow = NewsletterSubscription.CreateForUser(userId);
        enabledRow.AssignDigestSchedule(Hermes.Domain.ValueObjects.ScheduleWindow.EnsureForDigestScheduling([Weekdays.Monday], [new TimeOnly(9, 30)]));
        enabledRow.Enable();
        await newsStore.SetNewsAsync(enabledRow, CancellationToken.None);

        NewsletterSubscription disabledRow = NewsletterSubscription.CreateForUser(userId);
        disabledRow.AssignDigestSchedule(Hermes.Domain.ValueObjects.ScheduleWindow.EnsureForDigestScheduling([Weekdays.Monday], [new TimeOnly(9, 30)]));
        disabledRow.Disable();
        await newsStore.SetNewsAsync(disabledRow, CancellationToken.None);

        DateTime slotStartUtc = new(2026, 5, 4, 7, 0, 0, DateTimeKind.Utc);
        DateTime slotEndUtc = slotStartUtc.AddMinutes(1);

        List<(int NewsId, int UserId)> due = await newsStore.GetDueNewsScheduleForSlotAsync(
            Weekdays.Monday,
            9,
            30,
            slotStartUtc,
            slotEndUtc,
            CancellationToken.None);

        Assert.Contains((enabledRow.Id, userId), due);
        Assert.DoesNotContain((disabledRow.Id, userId), due);
    }
}
