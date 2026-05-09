using Hermes.Application.Ports;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NewsEntity = Hermes.Domain.Entities.News;

namespace Hermes.IntegrationTests.News;

/// <summary>
/// Validates MySQL-side due filtering (<c>JSON_CONTAINS</c> + <c>JSON_TABLE</c>) against real JSON stored by EF conversions.
/// </summary>
[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class NewsletterScheduleDueQueryIntegrationTests(MySqlApiFixture fixture)
{
    [Fact]
    public async Task GetDueNewsScheduleForSlotAsync_returns_only_rows_matching_weekday_and_time()
    {
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        IHermesDataStore store = scope.ServiceProvider.GetRequiredService<IHermesDataStore>();

        User user = new()
        {
            Id = 0,
            Name = "schedule-due-test",
            Email = $"sched-due-{Guid.NewGuid():N}@test.local",
            PasswordHash = "hash",
            IsEmailVerified = true,
        };
        await store.SetUserAsync(user, CancellationToken.None);
        int userId = user.Id;

        NewsEntity mondaySlot = new()
        {
            Id = 0,
            UserId = userId,
            SendOnWeekdays = [Weekdays.Monday],
            SendAtTimes = [new TimeOnly(9, 30)],
        };
        await store.SetNewsAsync(mondaySlot, CancellationToken.None);

        NewsEntity wrongWeekday = new()
        {
            Id = 0,
            UserId = userId,
            SendOnWeekdays = [Weekdays.Tuesday],
            SendAtTimes = [new TimeOnly(9, 30)],
        };
        await store.SetNewsAsync(wrongWeekday, CancellationToken.None);

        NewsEntity wrongTime = new()
        {
            Id = 0,
            UserId = userId,
            SendOnWeekdays = [Weekdays.Monday],
            SendAtTimes = [new TimeOnly(14, 0)],
        };
        await store.SetNewsAsync(wrongTime, CancellationToken.None);

        List<(int NewsId, int UserId)> due = await store.GetDueNewsScheduleForSlotAsync(
            Weekdays.Monday,
            9,
            30,
            CancellationToken.None);

        Assert.Contains((mondaySlot.Id, userId), due);
        Assert.DoesNotContain((wrongWeekday.Id, userId), due);
        Assert.DoesNotContain((wrongTime.Id, userId), due);
    }
}
