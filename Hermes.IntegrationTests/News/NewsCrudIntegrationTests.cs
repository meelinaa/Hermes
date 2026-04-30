using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Hermes.Domain.Enums;
using Hermes.IntegrationTests.Auth;
using Hermes.IntegrationTests.Infrastructure;

namespace Hermes.IntegrationTests.News;

/// <summary>
/// News REST CRUD under <c>api/v1/users/news</c> against MySQL + JWT enforcement (401/403) and ProblemDetails for 404/400 paths.
/// </summary>
[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class NewsCrudIntegrationTests(MySqlApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

    private static object MinimalNewsPayload(int id = 0, int userId = 0) => // This method constructs a minimal payload for creating or updating a news subscription, with default values for all required fields.
        new
        {
            id,
            userId,
            keywords = new[] { "integration-news" },
            category = new[] { (int)NewsCategory.Technology },
            languages = new[] { (int)Language.English },
            countries = new[] { (int)Country.Germany },
            sendOnWeekdays = new[] { (int)Weekdays.Monday },
            sendAtTimes = new[] { "09:00:00" },
        };

    [Fact]
    public async Task Crud_happy_path_create_list_get_update_delete()
    {
        using HttpClient client = fixture.Factory.CreateClient(); // Create a new HttpClient instance from the fixture's factory.
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client); // Register a new user and extract the user ID and email for authentication.
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword); // Log in with the registered user's credentials to obtain an access token for authorized requests.

        using HttpRequestMessage createReq = Authorized(HttpMethod.Post, "/api/v1/users/news", access); // Create an authorized POST request to the news creation endpoint, including the access token in the Authorization header.
        createReq.Content = JsonContent.Create(MinimalNewsPayload(), options: JsonWeb); // Set the request content to a JSON representation of the minimal news payload, which includes the user ID and other required fields for creating a news subscription.
        using HttpResponseMessage create = await client.SendAsync(createReq);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode); // Assert that the response status code is 200 OK, indicating that the news subscription was successfully created.
        using JsonDocument createdJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        Assert.Equal(userId, createdJson.RootElement.GetProperty("userId").GetInt32()); // Assert that the userId in the response matches the userId of the authenticated user, ensuring that the news subscription is associated with the correct user.
        int newsId = createdJson.RootElement.GetProperty("newsId").GetInt32();
        Assert.True(newsId > 0); // Assert that the newsId is a positive integer, indicating that the news subscription was successfully created.

        using HttpResponseMessage listResp = await client.SendAsync(Authorized(HttpMethod.Get, $"/api/v1/users/news/{userId}/list", access)); // Send an authorized GET request to the news listing endpoint for the authenticated user, including the access token in the Authorization header.
        listResp.EnsureSuccessStatusCode(); // Assert that the response status code indicates success (2xx), ensuring that the news listing was successfully retrieved.
        List<JsonElement>? list = await listResp.Content.ReadFromJsonAsync<List<JsonElement>>(options: JsonWeb);
        Assert.NotNull(list);
        Assert.Contains(list, e => e.GetProperty("id").GetInt32() == newsId);

        using HttpResponseMessage getOne = await client.SendAsync(
            Authorized(HttpMethod.Get, $"/api/v1/users/news/userId={userId}/newsId={newsId}", access)); // Send an authorized GET request to retrieve the specific news subscription by its ID, including the access token in the Authorization header.
        getOne.EnsureSuccessStatusCode(); // Assert that the response status code indicates success (2xx), ensuring that the specific news subscription was successfully retrieved by its ID.

        object updateBody = new // This object represents the payload for updating the news subscription, including the news ID, user ID, and updated values for keywords, category, languages, countries, sendOnWeekdays, and sendAtTimes. The updated values differ from the original payload to verify that the update operation correctly modifies the news subscription.
        {
            id = newsId,
            userId,
            keywords = new[] { "updated-keyword" },
            category = new[] { (int)NewsCategory.Business },
            languages = new[] { (int)Language.German },
            countries = new[] { (int)Country.Austria },
            sendOnWeekdays = new[] { (int)Weekdays.Friday },
            sendAtTimes = new[] { "18:00:00" },
        };
        using HttpRequestMessage putReq = Authorized(HttpMethod.Put, "/api/v1/users/news", access); // Create an authorized PUT request to the news update endpoint, including the access token in the Authorization header.
        putReq.Content = JsonContent.Create(updateBody, options: JsonWeb);
        using HttpResponseMessage putResp = await client.SendAsync(putReq);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        using HttpResponseMessage getUpdated = await client.SendAsync(
            Authorized(HttpMethod.Get, $"/api/v1/users/news/userId={userId}/newsId={newsId}", access)); // Send an authorized GET request to retrieve the updated news subscription by its ID, including the access token in the Authorization header.
        getUpdated.EnsureSuccessStatusCode();
        using JsonDocument updatedDoc = JsonDocument.Parse(await getUpdated.Content.ReadAsStringAsync());
        Assert.Equal("updated-keyword", updatedDoc.RootElement.GetProperty("keywords")[0].GetString());

        using HttpResponseMessage del = await client.SendAsync(
            Authorized(HttpMethod.Delete, $"/api/v1/users/news/userId={userId}/newsId={newsId}", access)); // Send an authorized DELETE request to delete the news subscription by its ID, including the access token in the Authorization header.
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        using HttpResponseMessage getMissing = await client.SendAsync(
            Authorized(HttpMethod.Get, $"/api/v1/users/news/userId={userId}/newsId={newsId}", access)); // Send an authorized GET request to verify that the news subscription has been deleted, including the access token in the Authorization header.
        Assert.Equal(HttpStatusCode.NotFound, getMissing.StatusCode);
    }

    [Fact]
    public async Task List_without_bearer_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, _) = await AuthIntegrationFlows.RegisterUserAsync(client); // Register a new user to obtain a valid user ID for the news listing endpoint, but do not authenticate to test the behavior of the endpoint when no bearer token is provided.

        using HttpResponseMessage response = await client.GetAsync($"/api/v1/users/news/{userId}/list");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_with_malformed_bearer_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);

        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/users/news/{userId}/list");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", JwtIntegrationTestTokens.MalformedJwtMaterial); // Set the Authorization header with a malformed JWT token to test the API's response to invalid authentication credentials.

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_for_foreign_user_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int victimId, _) = await AuthIntegrationFlows.RegisterUserAsync(client); // Register a "victim" user to obtain a valid user ID for the news listing endpoint, which will be used to test access control when an "attacker" user attempts to access the victim's news listing.
        (_, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client); // Register a second user to act as the "attacker" who will attempt to access the news listing of the "victim" user, testing the API's enforcement of resource ownership and authorization.
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DefaultPassword);

        using HttpResponseMessage response = await client.SendAsync(
            Authorized(HttpMethod.Get, $"/api/v1/users/news/{victimId}/list", access));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_with_foreign_body_userId_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int otherId, _) = await AuthIntegrationFlows.RegisterUserAsync(client); // Register a user to obtain a valid user ID that will be used in the news creation payload, but authenticate as a different user to test the API's enforcement of resource ownership when the user ID in the request body does not match the authenticated user's ID.
        (_, string selfEmail) = await AuthIntegrationFlows.RegisterUserAsync(client); // Register a second user to act as the "self" user who will attempt to create news with a foreign user ID, testing the API's enforcement of resource ownership and authorization.
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, selfEmail, AuthIntegrationFlows.DefaultPassword);

        using HttpRequestMessage req = Authorized(HttpMethod.Post, "/api/v1/users/news", access);
        req.Content = JsonContent.Create(MinimalNewsPayload(userId: otherId), options: JsonWeb);

        using HttpResponseMessage response = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Put_with_foreign_body_userId_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (int otherId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        using HttpRequestMessage createReq = Authorized(HttpMethod.Post, "/api/v1/users/news", access); // First, create a news subscription with the authenticated user's ID to obtain a valid news ID for the update test, ensuring that the news subscription is associated with the correct user.
        createReq.Content = JsonContent.Create(MinimalNewsPayload(), options: JsonWeb);
        using HttpResponseMessage create = await client.SendAsync(createReq); // Send the request to create the news subscription and ensure that it was successful, extracting the news ID from the response for use in the subsequent update test.
        create.EnsureSuccessStatusCode();
        using JsonDocument createdJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync()); // Parse the response content as JSON to extract the news ID of the created news subscription, which will be used in the update test to attempt to update the news subscription with a foreign user ID.
        int newsId = createdJson.RootElement.GetProperty("newsId").GetInt32();

        object updateBody = new // This object represents the payload for updating the news subscription, but it intentionally includes a foreign user ID (otherId) instead of the authenticated user's ID (userId) to test the API's enforcement of resource ownership and authorization when the user ID in the request body does not match the authenticated user's ID.
        {
            id = newsId,
            userId = otherId,
            keywords = new[] { "x" },
            category = new[] { (int)NewsCategory.Business },
            languages = new[] { (int)Language.English },
            countries = new[] { (int)Country.Germany },
            sendOnWeekdays = new[] { (int)Weekdays.Tuesday },
            sendAtTimes = new[] { "10:00:00" },
        };
        using HttpRequestMessage putReq = Authorized(HttpMethod.Put, "/api/v1/users/news", access);
        putReq.Content = JsonContent.Create(updateBody, options: JsonWeb);

        using HttpResponseMessage response = await client.SendAsync(putReq);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_by_id_unknown_news_returns_NotFound()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        using HttpResponseMessage response = await client.SendAsync( // Attempt to retrieve a news subscription by an ID that is unlikely to exist (int.MaxValue) for the authenticated user, testing the API's response when the requested resource is not found.
            Authorized(HttpMethod.Get, $"/api/v1/users/news/userId={userId}/newsId={int.MaxValue}", access));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_unknown_news_returns_NotFound()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        using HttpResponseMessage response = await client.SendAsync( // Attempt to delete a news subscription by an ID that is unlikely to exist (int.MaxValue) for the authenticated user, testing the API's response when the requested resource is not found.
            Authorized(HttpMethod.Delete, $"/api/v1/users/news/userId={userId}/newsId={int.MaxValue}", access));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_with_invalid_json_syntax_returns_BadRequest()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        using HttpRequestMessage req = Authorized(HttpMethod.Put, "/api/v1/users/news", access);
        req.Content = new StringContent("{ not-json", Encoding.UTF8, "application/json"); // Intentionally malformed JSON to test the API's response to invalid JSON syntax.

        using HttpResponseMessage response = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_with_wrong_json_type_for_keywords_returns_BadRequest()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        object badBody = new // This object represents a payload for updating a news subscription, but it intentionally sets the "keywords" property to a string instead of an array of strings, which is expected by the API.
        {
            id = 1,
            userId,
            keywords = "must-be-array", // Incorrect type for keywords.
            category = new[] { (int)NewsCategory.Technology },
            languages = new[] { (int)Language.English },
            countries = new[] { (int)Country.Germany },
            sendOnWeekdays = new[] { (int)Weekdays.Monday },
            sendAtTimes = new[] { "09:00:00" },
        };
        string json = JsonSerializer.Serialize(badBody, JsonWeb);

        using HttpRequestMessage req = Authorized(HttpMethod.Put, "/api/v1/users/news", access);
        req.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.SendAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_all_for_foreign_user_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int victimId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (_, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DefaultPassword); // Authenticate as the "attacker" user and attempt to delete all news subscriptions for the "victim" user, testing the API's enforcement of resource ownership and authorization when attempting to perform a bulk delete operation on another user's resources.

        using HttpResponseMessage response = await client.SendAsync(
            Authorized(HttpMethod.Delete, $"/api/v1/users/news/userId={victimId}/delete/all", access));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string relativeUri, string accessToken)
    {
        HttpRequestMessage request = new(method, relativeUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }
}
