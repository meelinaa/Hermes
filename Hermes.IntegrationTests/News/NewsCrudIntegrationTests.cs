using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Hermes.Domain.Enums;
using Hermes.IntegrationTests.Auth;
using Hermes.IntegrationTests.Infrastructure;
using Xunit;

namespace Hermes.IntegrationTests.News;

/// <summary>
/// Contains end-to-end integration tests for newsletter subscriptions,
/// verifying CRUD operations, pagination, payload casing, and strict tenant ownership isolation.
/// </summary>
[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class NewsCrudIntegrationTests(MySqlApiFixture fixture)
{
    private static readonly JsonSerializerOptions _jsonWeb = new(JsonSerializerDefaults.Web);

    private static object MinimalNewsCreatePayload() =>
        new
        {
            keywords = new[] { "integration-news" },
            category = new[] { (int)NewsCategory.Technology },
            languages = new[] { (int)Language.English },
            countries = new[] { (int)Country.Germany },
            sendOnWeekdays = new[] { (int)Weekdays.Monday },
            sendAtTimes = new[] { "09:00:00" },
        };

    /// <summary>
    /// Tests that the response payload for creating a newsletter subscription uses camelCase property keys.
    /// </summary>
    [Fact]
    public async Task Post_news_contract_uses_userId_and_newsId_camelCase()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpRequestMessage req = Authorized(HttpMethod.Post, "/api/v1/users/newsletter-subscriptions", access);
        req.Content = JsonContent.Create(MinimalNewsCreatePayload(), options: _jsonWeb);
        using HttpResponseMessage resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        using JsonDocument doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        Assert.True(root.TryGetProperty("userId", out JsonElement u) && userId == u.GetInt32(), "Expect userId (camelCase) matching JWT subject.");
        Assert.True(root.TryGetProperty("subscriptionId", out JsonElement n) && n.GetInt32() > 0, "Expect subscriptionId (camelCase).");
        Assert.False(root.TryGetProperty("UserId", out JsonElement _), "Pascal-case keys break Web defaults.");
        Assert.False(root.TryGetProperty("SubscriptionId", out JsonElement _), "Pascal-case keys break Web defaults.");
    }

    /// <summary>
    /// Tests the full CRUD lifecycle of newsletter subscriptions: Create, List, Get, Update, and Delete.
    /// </summary>
    [Fact]
    public async Task Crud_happy_path_create_list_get_update_delete()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpRequestMessage createReq = Authorized(HttpMethod.Post, "/api/v1/users/newsletter-subscriptions", access);
        createReq.Content = JsonContent.Create(MinimalNewsCreatePayload(), options: _jsonWeb);
        using HttpResponseMessage create = await client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        using JsonDocument createdJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        Assert.Equal(userId, createdJson.RootElement.GetProperty("userId").GetInt32());
        int newsId = createdJson.RootElement.GetProperty("subscriptionId").GetInt32();
        Assert.True(newsId > 0);

        using HttpResponseMessage listResp = await client.SendAsync(Authorized(HttpMethod.Get, $"/api/v1/users/{userId}/newsletter-subscriptions", access));
        listResp.EnsureSuccessStatusCode();
        using JsonDocument listDoc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync());
        JsonElement items = listDoc.RootElement.GetProperty("items");
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        bool found = false;
        foreach (JsonElement item in items.EnumerateArray())
        {
            if (item.GetProperty("id").GetInt32() == newsId)
            {
                found = true;
                break;
            }
        }

        Assert.True(found);

        using HttpResponseMessage getOne = await client.SendAsync(
            Authorized(HttpMethod.Get, $"/api/v1/users/{userId}/newsletter-subscriptions/{newsId}", access));
        getOne.EnsureSuccessStatusCode();
        using (JsonDocument oneDoc = JsonDocument.Parse(await getOne.Content.ReadAsStringAsync()))
        {
            Assert.True(
                oneDoc.RootElement.TryGetProperty("isEnabled", out JsonElement en) && en.GetBoolean(),
                "New row should default to isEnabled true.");
        }

        object updateBody = new
        {
            id = newsId,
            keywords = new[] { "updated-keyword" },
            category = new[] { (int)NewsCategory.Business },
            languages = new[] { (int)Language.German },
            countries = new[] { (int)Country.Austria },
            sendOnWeekdays = new[] { (int)Weekdays.Friday },
            sendAtTimes = new[] { "18:00:00" },
            isEnabled = false,
        };
        using HttpRequestMessage putReq = Authorized(HttpMethod.Put, "/api/v1/users/newsletter-subscriptions", access);
        putReq.Content = JsonContent.Create(updateBody, options: _jsonWeb);
        using HttpResponseMessage putResp = await client.SendAsync(putReq);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        using HttpResponseMessage getUpdated = await client.SendAsync(
            Authorized(HttpMethod.Get, $"/api/v1/users/{userId}/newsletter-subscriptions/{newsId}", access));
        getUpdated.EnsureSuccessStatusCode();
        using JsonDocument updatedDoc = JsonDocument.Parse(await getUpdated.Content.ReadAsStringAsync());
        Assert.Equal("updated-keyword", updatedDoc.RootElement.GetProperty("keywords")[0].GetString());
        Assert.True(
            updatedDoc.RootElement.TryGetProperty("isEnabled", out JsonElement off) && !off.GetBoolean(),
            "PUT should persist isEnabled false.");

        using HttpResponseMessage del = await client.SendAsync(
            Authorized(HttpMethod.Delete, $"/api/v1/users/{userId}/newsletter-subscriptions/{newsId}", access));
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        using HttpResponseMessage getMissing = await client.SendAsync(
            Authorized(HttpMethod.Get, $"/api/v1/users/{userId}/newsletter-subscriptions/{newsId}", access));
        Assert.Equal(HttpStatusCode.NotFound, getMissing.StatusCode);
    }

    /// <summary>
    /// Tests that querying subscriptions without an authorization header returns HTTP 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task List_without_bearer_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);

        using HttpResponseMessage response = await client.GetAsync($"/api/v1/users/{userId}/newsletter-subscriptions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Tests that querying subscriptions with a malformed JWT token returns HTTP 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task List_with_malformed_bearer_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);

        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/users/{userId}/newsletter-subscriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", JwtIntegrationTestTokens.MALFORMED_JWT_MATERIAL);

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Tests that attempting to list subscriptions belonging to another user returns HTTP 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task List_for_foreign_user_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int victimId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (_, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpResponseMessage response = await client.SendAsync(
            Authorized(HttpMethod.Get, $"/api/v1/users/{victimId}/newsletter-subscriptions", access));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Tests that attempting to get a specific subscription belonging to another user returns HTTP 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task Get_foreign_news_by_id_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int victimId, string victimEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string victimAccess = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, victimEmail, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpRequestMessage victimCreate = Authorized(HttpMethod.Post, "/api/v1/users/newsletter-subscriptions", victimAccess);
        victimCreate.Content = JsonContent.Create(MinimalNewsCreatePayload(), options: _jsonWeb);
        using HttpResponseMessage victimResp = await client.SendAsync(victimCreate);
        victimResp.EnsureSuccessStatusCode();
        using JsonDocument doc = JsonDocument.Parse(await victimResp.Content.ReadAsStringAsync());
        int newsId = doc.RootElement.GetProperty("subscriptionId").GetInt32();

        (_, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string attackerAccess = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpResponseMessage response = await client.SendAsync(
            Authorized(HttpMethod.Get, $"/api/v1/users/{victimId}/newsletter-subscriptions/{newsId}", attackerAccess));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Tests that an extraneous userId passed in POST body is safely ignored and overridden by JWT identity.
    /// </summary>
    [Fact]
    public async Task Post_extraneous_userId_property_in_body_is_ignored_for_owner()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int otherId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (int selfId, string selfEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, selfEmail, AuthIntegrationFlows.DEFAULT_PASSWORD);

        object bodyWithIgnoredUserId = new
        {
            userId = otherId,
            keywords = new[] { "integration-news" },
            category = new[] { (int)NewsCategory.Technology },
            languages = new[] { (int)Language.English },
            countries = new[] { (int)Country.Germany },
            sendOnWeekdays = new[] { (int)Weekdays.Monday },
            sendAtTimes = new[] { "09:00:00" },
        };
        using HttpRequestMessage req = Authorized(HttpMethod.Post, "/api/v1/users/newsletter-subscriptions", access);
        req.Content = JsonContent.Create(bodyWithIgnoredUserId, options: _jsonWeb);

        using HttpResponseMessage response = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(selfId, json.RootElement.GetProperty("userId").GetInt32());
    }

    /// <summary>
    /// Tests that attempting to update a subscription belonging to another user returns HTTP 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task Put_when_news_owned_by_another_user_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string victimEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string victimAccess = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, victimEmail, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpRequestMessage victimCreate = Authorized(HttpMethod.Post, "/api/v1/users/newsletter-subscriptions", victimAccess);
        victimCreate.Content = JsonContent.Create(MinimalNewsCreatePayload(), options: _jsonWeb);
        using HttpResponseMessage victimNewsResp = await client.SendAsync(victimCreate);
        victimNewsResp.EnsureSuccessStatusCode();
        using JsonDocument victimJson = JsonDocument.Parse(await victimNewsResp.Content.ReadAsStringAsync());
        int victimNewsId = victimJson.RootElement.GetProperty("subscriptionId").GetInt32();

        (_, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string attackerAccess = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DEFAULT_PASSWORD);

        object attackerUpdate = new
        {
            id = victimNewsId,
            keywords = new[] { "stolen-update" },
            category = new[] { (int)NewsCategory.Business },
            languages = new[] { (int)Language.English },
            countries = new[] { (int)Country.Germany },
            sendOnWeekdays = new[] { (int)Weekdays.Tuesday },
            sendAtTimes = new[] { "10:00:00" },
        };
        using HttpRequestMessage putReq = Authorized(HttpMethod.Put, "/api/v1/users/newsletter-subscriptions", attackerAccess);
        putReq.Content = JsonContent.Create(attackerUpdate, options: _jsonWeb);

        using HttpResponseMessage response = await client.SendAsync(putReq);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Tests that requesting a non-existent subscription ID returns HTTP 404 NotFound.
    /// </summary>
    [Fact]
    public async Task Get_by_id_unknown_news_returns_NotFound()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpResponseMessage response = await client.SendAsync(
            Authorized(HttpMethod.Get, $"/api/v1/users/{userId}/newsletter-subscriptions/{int.MaxValue}", access));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Tests that deleting a non-existent subscription ID returns HTTP 404 NotFound.
    /// </summary>
    [Fact]
    public async Task Delete_unknown_news_returns_NotFound()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpResponseMessage response = await client.SendAsync(
            Authorized(HttpMethod.Delete, $"/api/v1/users/{userId}/newsletter-subscriptions/{int.MaxValue}", access));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Tests that attempting to delete all subscriptions belonging to another user returns HTTP 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task Delete_all_for_foreign_user_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int victimId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (_, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpResponseMessage response = await client.SendAsync(
            Authorized(HttpMethod.Delete, $"/api/v1/users/{victimId}/newsletter-subscriptions/all", access));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string relativeUri, string accessToken)
    {
        HttpRequestMessage request = new(method, relativeUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }
}
