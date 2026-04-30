using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Hermes.IntegrationTests.Auth;
using Hermes.IntegrationTests.Infrastructure;

namespace Hermes.IntegrationTests.NotificationLogs;

/// <summary>
/// POST endpoint under <c>api/v1/users/{userId}/notification-logs</c>; verifies inserts plus auth/binding failures (401/403/400).
/// </summary>
[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class NotificationLogsIntegrationTests(MySqlApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

    private static object MinimalLogBody(int id = 0, int userId = 0) => // This method constructs a minimal notification log entry with default values for all fields except for the userId.
        new
        {
            id,
            userId,
            newsId = (int?)null,
            sentAt = DateTime.UtcNow,
            status = "Pending",
            channel = "Email",
            errorMessage = (string?)null,
            retryCount = 0,
            nextRetryAt = (DateTime?)null,
        };

    [Fact]
    public async Task Post_own_notification_log_returns_OK_and_persisted_id()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        using HttpRequestMessage req = new(HttpMethod.Post, $"/api/v1/users/{userId}/notification-logs");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        req.Content = JsonContent.Create(MinimalLogBody(), options: JsonWeb); // Create the POST request to create a new notification log entry for the authenticated user, with a minimal body containing default values, and capture the response to verify that the API successfully creates the log entry and returns the expected data in the response (e.g., generated ID, userId, status, channel).

        using HttpResponseMessage response = await client.SendAsync(req); // Send the POST request to create a new notification log entry and capture the response.

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("id").GetInt32() > 0);
        Assert.Equal(userId, json.RootElement.GetProperty("userId").GetInt32());
        Assert.Equal("Pending", json.RootElement.GetProperty("status").GetString(), ignoreCase: true);
        Assert.Equal("Email", json.RootElement.GetProperty("channel").GetString(), ignoreCase: true);
    }

    [Fact]
    public async Task Post_with_matching_explicit_body_userId_returns_OK()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        using HttpRequestMessage req = new(HttpMethod.Post, $"/api/v1/users/{userId}/notification-logs");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        req.Content = JsonContent.Create(MinimalLogBody(userId: userId), options: JsonWeb); // Create the POST request with the userId explicitly set in the body, matching the route parameter, and capture the response to verify that the API accepts this redundant but consistent information without error.

        using HttpResponseMessage response = await client.SendAsync(req); // Send the POST request with the explicit userId in the body and capture the response.

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(userId, json.RootElement.GetProperty("userId").GetInt32());
    }

    [Fact]
    public async Task Post_without_bearer_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/users/{userId}/notification-logs",
            MinimalLogBody(),
            options: JsonWeb);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_with_malformed_bearer_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);

        using HttpRequestMessage req = new(HttpMethod.Post, $"/api/v1/users/{userId}/notification-logs");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", JwtIntegrationTestTokens.MalformedJwtMaterial);
        req.Content = JsonContent.Create(MinimalLogBody(), options: JsonWeb);

        using HttpResponseMessage response = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_for_foreign_user_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int victimId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (_, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DefaultPassword);

        using HttpRequestMessage req = new(HttpMethod.Post, $"/api/v1/users/{victimId}/notification-logs");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        req.Content = JsonContent.Create(MinimalLogBody(), options: JsonWeb);

        using HttpResponseMessage response = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_with_body_userId_not_matching_route_returns_BadRequest()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int victimId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (int attackerId, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DefaultPassword);

        using HttpRequestMessage req = new(HttpMethod.Post, $"/api/v1/users/{attackerId}/notification-logs");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        req.Content = JsonContent.Create(MinimalLogBody(userId: victimId), options: JsonWeb);

        using HttpResponseMessage response = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_with_invalid_json_returns_BadRequest()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        using HttpRequestMessage req = new(HttpMethod.Post, $"/api/v1/users/{userId}/notification-logs");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        req.Content = new StringContent("{ not-json", Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
