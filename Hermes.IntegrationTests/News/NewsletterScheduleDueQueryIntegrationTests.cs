using Hermes.Application.Ports;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NewsEntity = Hermes.Domain.Entities.News;

namespace Hermes.IntegrationTests.News;

/// <summary>
/// Validates MySQL-side due filtering (<c>JSON_SEARCH</c> / <c>JSON_VALID</c>) against real JSON stored by EF conversions.
/// </summary>
[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class NewsletterScheduleDueQueryIntegrationTests(MySqlApiFixture fixture)
{
    [Fact]
    public async Task GetDueNewsScheduleForSlotAsync_returns_only_rows_matching_weekday_and_time()
    {
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        IUserStore users = scope.ServiceProvider.GetRequiredService<IUserStore>();
        INewsStore newsStore = scope.ServiceProvider.GetRequiredService<INewsStore>();

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

        NewsEntity mondaySlot = new()
        {
            Id = 0,
            UserId = userId,
            SendOnWeekdays = [Weekdays.Monday],
            SendAtTimes = [new TimeOnly(9, 30)],
        };
        await newsStore.SetNewsAsync(mondaySlot, CancellationToken.None);

        NewsEntity wrongWeekday = new()
        {
            Id = 0,
            UserId = userId,
            SendOnWeekdays = [Weekdays.Tuesday],
            SendAtTimes = [new TimeOnly(9, 30)],
        };
        await newsStore.SetNewsAsync(wrongWeekday, CancellationToken.None);

        NewsEntity wrongTime = new()
        {
            Id = 0,
            UserId = userId,
            SendOnWeekdays = [Weekdays.Monday],
            SendAtTimes = [new TimeOnly(14, 0)],
        };
        await newsStore.SetNewsAsync(wrongTime, CancellationToken.None);

        // Direct INewsStore inserts leave NextDigestSlotUtc null, so matching uses the JSON_SEARCH path only;
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
}
