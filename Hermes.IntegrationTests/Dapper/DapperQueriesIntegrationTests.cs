using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hermes.Application.DTOs.User;
using Hermes.Application.Ports.Outbound;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hermes.IntegrationTests.Dapper;

[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class DapperQueriesIntegrationTests(MySqlApiFixture fixture)
{
    private static readonly JsonSerializerOptions _jsonWeb = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task UserDapperQueries_Should_Retrieve_UserScope_And_Check_Existence()
    {
        // Arrange: create a user via HTTP API
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"dapper-{Guid.NewGuid():N}@integration.hermes";
        string name = "Dapper Test User";
        object dto = new
        {
            id = 0,
            name,
            email,
            password = AuthIntegrationFlows.DEFAULT_PASSWORD,
            isEmailVerified = true,
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/users", dto, options: _jsonWeb);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        int userId = json.RootElement.GetProperty("userId").GetInt32();

        // Act & Assert via Dapper Read Queries
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        IUserReadQueries userQueries = scope.ServiceProvider.GetRequiredService<IUserReadQueries>();

        // 1. Get by ID
        UserScopeDto? byId = await userQueries.GetUserScopeByIdAsync(userId);
        Assert.NotNull(byId);
        Assert.Equal(userId, byId.UserId);
        Assert.Equal(email, byId.Email);
        Assert.Equal(name, byId.Name);

        // 2. Get by Email
        UserScopeDto? byEmail = await userQueries.GetUserScopeByEmailAsync(email);
        Assert.NotNull(byEmail);
        Assert.Equal(userId, byEmail.UserId);
        Assert.Equal(name, byEmail.Name);

        // 3. Get by Name
        UserScopeDto? byName = await userQueries.GetUserScopeByNameAsync(name);
        Assert.NotNull(byName);
        Assert.Equal(userId, byName.UserId);

        // 4. Exists by Email
        bool exists = await userQueries.ExistsByEmailAsync(email);
        Assert.True(exists);

        bool notExists = await userQueries.ExistsByEmailAsync("non-existing@domain.invalid");
        Assert.False(notExists);
    }

    [Fact]
    public async Task NewsletterDapperQueries_Should_Retrieve_Active_Subscription_Counts_And_Ids()
    {
        // Arrange: seed user and newsletter subscriptions directly in DbContext
        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        HermesDbContext db = scope.ServiceProvider.GetRequiredService<HermesDbContext>();

        var user = new User
        {
            Id = new UserId(0),
            Name = "Dapper News Subscriber",
            Email = Email.Parse($"subscriber-{Guid.NewGuid():N}@integration.hermes"),
            PasswordHash = "$2a$11$placeholder"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var sub1 = NewsletterSubscription.CreateForUser(user.Id);
        sub1.UpdateFilters(["tech"], [NewsCategory.Technology], [Language.German], [Country.Germany]);
        sub1.SetSchedule([Weekdays.Monday], [new TimeOnly(8, 0)]);

        var sub2 = NewsletterSubscription.CreateForUser(user.Id);
        sub2.UpdateFilters(["science"], [NewsCategory.Science], [Language.English], [Country.USA]);
        sub2.SetSchedule([Weekdays.Friday], [new TimeOnly(18, 0)]);
        sub2.Disable(); // disabled subscription

        db.NewsletterSubscriptions.AddRange(sub1, sub2);
        await db.SaveChangesAsync();

        // Act & Assert via Dapper Read Queries
        INewsletterReadQueries newsQueries = scope.ServiceProvider.GetRequiredService<INewsletterReadQueries>();

        int activeCount = await newsQueries.GetActiveSubscriptionCountByUserIdAsync(user.Id.Value);
        Assert.Equal(1, activeCount);

        IReadOnlyList<int> activeIds = await newsQueries.GetActiveSubscriptionIdsByUserIdAsync(user.Id.Value);
        Assert.Single(activeIds);
        Assert.Equal(sub1.Id.Value, activeIds[0]);
    }
}
