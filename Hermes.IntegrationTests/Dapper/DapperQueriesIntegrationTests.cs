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

/// <summary>
/// Contains integration tests for Dapper-based high-performance CQRS read queries,
/// verifying direct SQL lookups, case-insensitivity, whitespace trimming, and SQL-injection safety.
/// </summary>
[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class DapperQueriesIntegrationTests(MySqlApiFixture fixture)
{
    private static readonly JsonSerializerOptions _jsonWeb = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Tests that <see cref="IUserReadQueries"/> correctly fetches user scopes by ID, email, and name,
    /// and accurately reports account existence.
    /// </summary>
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

    /// <summary>
    /// Tests that <see cref="IUserReadQueries"/> handles case-insensitive email matching,
    /// whitespace trimming, and potential SQL-injection characters safely.
    /// </summary>
    [Fact]
    public async Task UserDapperQueries_Should_HandleCaseInsensitivity_And_SqlInjectionResilience()
    {
        // Arrange
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"cased-{Guid.NewGuid():N}@integration.hermes";
        string name = "Cased User";
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

        using IServiceScope scope = fixture.Factory.Services.CreateScope();
        IUserReadQueries userQueries = scope.ServiceProvider.GetRequiredService<IUserReadQueries>();

        // Act 1: Mixed case and whitespace email lookup
        string upperEmailWithSpaces = $"   {email.ToUpperInvariant()}   ";
        UserScopeDto? byUpperEmail = await userQueries.GetUserScopeByEmailAsync(upperEmailWithSpaces);
        bool existsUpper = await userQueries.ExistsByEmailAsync(upperEmailWithSpaces);

        // Assert 1
        Assert.NotNull(byUpperEmail);
        Assert.Equal(email, byUpperEmail!.Email);
        Assert.True(existsUpper);

        // Act 2: SQL Injection Attempt string
        string sqlInjectionAttempt = "' OR '1'='1' --";
        UserScopeDto? byInjection = await userQueries.GetUserScopeByEmailAsync(sqlInjectionAttempt);
        bool existsInjection = await userQueries.ExistsByEmailAsync(sqlInjectionAttempt);

        // Assert 2
        Assert.Null(byInjection);
        Assert.False(existsInjection);

        // Act 3: Null and whitespace boundary handling
        UserScopeDto? nullEmail = await userQueries.GetUserScopeByEmailAsync(null!);
        UserScopeDto? whitespaceName = await userQueries.GetUserScopeByNameAsync("   ");
        bool nullExists = await userQueries.ExistsByEmailAsync(null!);

        // Assert 3
        Assert.Null(nullEmail);
        Assert.Null(whitespaceName);
        Assert.False(nullExists);
    }

    /// <summary>
    /// Tests that <see cref="INewsletterReadQueries"/> retrieves active subscription counts and IDs correctly,
    /// while filtering out disabled subscriptions.
    /// </summary>
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

        // Boundary: User with 0 subscriptions
        int nonExistentCount = await newsQueries.GetActiveSubscriptionCountByUserIdAsync(999999);
        IReadOnlyList<int> nonExistentIds = await newsQueries.GetActiveSubscriptionIdsByUserIdAsync(999999);
        Assert.Equal(0, nonExistentCount);
        Assert.Empty(nonExistentIds);
    }
}
