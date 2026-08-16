using System.Net;
using System.Net.Http.Headers;
using Hermes.Domain.Entities;
using Hermes.Domain.Enums;
using Hermes.Domain.ValueObjects;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.IntegrationTests.Auth;
using Hermes.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hermes.IntegrationTests.Users;

/// <summary>
/// Contains integration tests verifying relational cascade delete behaviors and database foreign key integrity
/// across Users, NewsletterSubscriptions, RefreshTokens, and NotificationLogs in MySQL.
/// </summary>
[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class UserCascadeDeleteIntegrationTests(MySqlApiFixture fixture)
{
    /// <summary>
    /// Tests that deleting a user account cleanly cascade-deletes all associated subscriptions,
    /// refresh tokens, and notification logs from MySQL without foreign key violations or orphan records.
    /// </summary>
    [Fact]
    public async Task Deleting_User_CascadeDeletes_All_Subscriptions_RefreshTokens_And_NotificationLogs()
    {
        // Arrange: Create user and authenticated client
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string accessToken = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        UserId typedUserId = new(userId);

        // Seed child entities in MySQL directly
        using (IServiceScope scope = fixture.Factory.Services.CreateScope())
        {
            HermesDbContext db = scope.ServiceProvider.GetRequiredService<HermesDbContext>();

            // 1. Add 2 Newsletter Subscriptions
            NewsletterSubscription sub1 = NewsletterSubscription.CreateForUser(typedUserId);
            sub1.UpdateFilters(["tech"], [NewsCategory.Technology], [Language.English], [Country.USA]);
            sub1.SetSchedule([Weekdays.Monday], [new TimeOnly(8, 0)]);

            NewsletterSubscription sub2 = NewsletterSubscription.CreateForUser(typedUserId);
            sub2.UpdateFilters(["business"], [NewsCategory.Business], [Language.German], [Country.Germany]);
            sub2.SetSchedule([Weekdays.Wednesday], [new TimeOnly(12, 0)]);

            db.NewsletterSubscriptions.AddRange(sub1, sub2);

            // 2. Add Notification Logs
            NotificationLog log1 = new(
                typedUserId,
                null,
                "user@integration.hermes",
                NotificationChannel.Email,
                NotificationStatus.Sent,
                null,
                DateTime.UtcNow);

            db.NotificationLogs.Add(log1);
            await db.SaveChangesAsync();
        }

        // Verify that child records exist before deletion
        using (IServiceScope scope = fixture.Factory.Services.CreateScope())
        {
            HermesDbContext db = scope.ServiceProvider.GetRequiredService<HermesDbContext>();
            Assert.True(await db.NewsletterSubscriptions.AnyAsync(s => s.UserId == typedUserId));
            Assert.True(await db.NotificationLogs.AnyAsync(n => n.UserId == typedUserId));
            Assert.True(await db.RefreshTokens.AnyAsync(t => t.UserId == typedUserId));
        }

        // Act: Delete user account via API
        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/api/v1/users/{userId}");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest);

        // Assert: HTTP 200 OK
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        // Assert: All child rows must be gone from MySQL
        using (IServiceScope scope = fixture.Factory.Services.CreateScope())
        {
            HermesDbContext db = scope.ServiceProvider.GetRequiredService<HermesDbContext>();

            bool userExists = await db.Users.AnyAsync(u => u.Id == typedUserId);
            bool subsExist = await db.NewsletterSubscriptions.AnyAsync(s => s.UserId == typedUserId);
            bool logsExist = await db.NotificationLogs.AnyAsync(n => n.UserId == typedUserId);
            bool tokensExist = await db.RefreshTokens.AnyAsync(t => t.UserId == typedUserId);

            Assert.False(userExists, "User record must be deleted.");
            Assert.False(subsExist, "Newsletter subscriptions must be cascade-deleted.");
            Assert.False(logsExist, "Notification logs must be cascade-deleted.");
            Assert.False(tokensExist, "Refresh tokens must be cascade-deleted.");
        }
    }

    /// <summary>
    /// Tests that deleting one user does not affect or inadvertently delete subscriptions or tokens of other users.
    /// </summary>
    [Fact]
    public async Task Deleting_One_User_Does_Not_Affect_Other_Users_Data()
    {
        // Arrange: Create two separate users
        using HttpClient client = fixture.Factory.CreateClient();
        (int victimId, string victimEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (int bystanderId, string bystanderEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);

        string victimToken = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, victimEmail, AuthIntegrationFlows.DEFAULT_PASSWORD);
        string bystanderToken = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, bystanderEmail, AuthIntegrationFlows.DEFAULT_PASSWORD);

        UserId victimTypedId = new(victimId);
        UserId bystanderTypedId = new(bystanderId);

        using (IServiceScope scope = fixture.Factory.Services.CreateScope())
        {
            HermesDbContext db = scope.ServiceProvider.GetRequiredService<HermesDbContext>();

            NewsletterSubscription bystanderSub = NewsletterSubscription.CreateForUser(bystanderTypedId);
            bystanderSub.UpdateFilters(["science"], [NewsCategory.Science], [Language.English], [Country.USA]);
            bystanderSub.SetSchedule([Weekdays.Friday], [new TimeOnly(18, 0)]);

            db.NewsletterSubscriptions.Add(bystanderSub);
            await db.SaveChangesAsync();
        }

        // Act: Delete only victim user
        using HttpRequestMessage deleteRequest = new(HttpMethod.Delete, $"/api/v1/users/{victimId}");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", victimToken);
        using HttpResponseMessage deleteResponse = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        // Assert: Bystander user and data still intact
        using (IServiceScope scope = fixture.Factory.Services.CreateScope())
        {
            HermesDbContext db = scope.ServiceProvider.GetRequiredService<HermesDbContext>();

            bool bystanderExists = await db.Users.AnyAsync(u => u.Id == bystanderTypedId);
            bool bystanderSubExists = await db.NewsletterSubscriptions.AnyAsync(s => s.UserId == bystanderTypedId);
            bool bystanderTokenExists = await db.RefreshTokens.AnyAsync(t => t.UserId == bystanderTypedId);

            Assert.True(bystanderExists, "Bystander user must remain intact.");
            Assert.True(bystanderSubExists, "Bystander subscription must remain intact.");
            Assert.True(bystanderTokenExists, "Bystander refresh token must remain intact.");
        }
    }
}
