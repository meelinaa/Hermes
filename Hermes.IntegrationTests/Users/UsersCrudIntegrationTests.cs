using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Hermes.IntegrationTests.Infrastructure;


namespace Hermes.IntegrationTests.Users;

/// <summary>
/// <see cref="Hermes.Api.Controllers.UsersController"/> integration coverage: CRUD-style flows, self-service 404 after delete,
/// and cross-account access denied (403) on GET/PUT/DELETE and GET-by-email.
/// </summary>
[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class UsersCrudIntegrationTests(MySqlApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Register_anonymous_returns_OK_and_user_scope()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"users-{Guid.NewGuid():N}@integration.hermes"; // Generate a unique email for the new user to ensure test isolation and avoid conflicts with existing users in the database; this allows the test to reliably create a new user account without interference from previous test runs or other data.
        object dto = new
        {
            id = 0,
            name = "Integration Users Test",
            email,
            passwordHash = AuthIntegrationFlows.DefaultPassword,
            isEmailVerified = false,
            twoFactorCode = (string?)null,
            twoFactorExpiry = (DateTime?)null,
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/users", dto, options: JsonWeb); // Send a POST request to the user registration endpoint with the DTO as the JSON body; this should create a new user in the system and return a response containing the user's ID, email, and name, which are asserted in the test to confirm successful registration and correct data handling by the API.

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("userId").GetInt32() > 0);
        Assert.Equal(email, json.RootElement.GetProperty("email").GetString());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Register_without_password_returns_BadRequest()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        object dto = new
        {
            id = 0,
            name = "No Password User",
            email = $"nopwd-{Guid.NewGuid():N}@integration.hermes",
            passwordHash = "", // An empty passwordHash is used here.
            isEmailVerified = false,
            twoFactorCode = (string?)null,
            twoFactorExpiry = (DateTime?)null,
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/users", dto, options: JsonWeb);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_own_profile_returns_OK()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        using HttpResponseMessage response = await client.SendAsync(AuthorizedGet($"/api/v1/users/{userId}", access)); // Send an authorized GET request to the user profile endpoint using the access token obtained from logging in; this should return the user's profile information, which is then asserted to confirm that the correct data is returned and that the authentication mechanism is functioning properly.

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(userId, json.RootElement.GetProperty("userId").GetInt32());
        Assert.Equal(email, json.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Get_own_profile_by_email_returns_OK()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        string path = $"/api/v1/users/by-email/{Uri.EscapeDataString(email)}"; // Construct the API endpoint path for retrieving the user profile by email, ensuring that the email is properly URL-encoded to handle any special characters; this allows the test to verify that the API correctly processes email-based queries and returns the expected user information when accessed with valid authentication.
        using HttpResponseMessage response = await client.SendAsync(AuthorizedGet(path, access));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(userId, json.RootElement.GetProperty("userId").GetInt32());
    }

    [Fact]
    public async Task Update_own_profile_returns_OK_and_reflected_on_get()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        string newEmail = $"renamed-{Guid.NewGuid():N}@integration.hermes";
        object body = new
        {
            id = userId,
            name = "Renamed Integration User",
            email = newEmail, // Update the email to a new unique value 
            newPassword = (string?)null,
            currentPassword = (string?)null,
        };

        using HttpRequestMessage put = new(HttpMethod.Put, "/api/v1/users"); // Create an HTTP PUT request to the user update endpoint with the updated profile information in the body.
        put.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        put.Content = JsonContent.Create(body, options: JsonWeb);

        using HttpResponseMessage putResp = await client.SendAsync(put);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        using HttpResponseMessage getResp = await client.SendAsync(AuthorizedGet($"/api/v1/users/{userId}", access));
        getResp.EnsureSuccessStatusCode();
        using JsonDocument got = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync()); // Send an authorized GET request to retrieve the updated user profile and assert that the changes are reflected in the response, confirming that the update operation was successful and that the API correctly processes profile modifications.
        Assert.Equal("Renamed Integration User", got.RootElement.GetProperty("name").GetString());
        Assert.Equal(newEmail, got.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Delete_own_user_then_get_returns_NotFound()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        using HttpResponseMessage deleteResp = await client.SendAsync(AuthorizedDelete($"/api/v1/users/{userId}", access)); // Send an authorized DELETE request to the user deletion endpoint to remove the user's account; this should return a success status code, and subsequent attempts to retrieve the deleted user profile should result in a NotFound response, confirming that the deletion was effective and that the API correctly handles resource removal.
        Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);

        using HttpResponseMessage getResp = await client.SendAsync(AuthorizedGet($"/api/v1/users/{userId}", access)); // Send an authorized GET request to verify that the user profile has been deleted; this should return a NotFound status code, confirming that the deletion was successful and that the API correctly handles attempts to access removed resources.
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task Get_foreign_user_by_id_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int victimId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (_, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string attackerAccess = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DefaultPassword);

        using HttpResponseMessage response = await client.SendAsync(AuthorizedGet($"/api/v1/users/{victimId}", attackerAccess));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Put_foreign_user_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int victimId, string victimEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (_, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string attackerAccess = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DefaultPassword);

        object body = new
        {
            id = victimId,
            name = "Attacker Try",
            email = victimEmail,
            newPassword = (string?)null,
            currentPassword = (string?)null,
        };

        using HttpRequestMessage put = new(HttpMethod.Put, "/api/v1/users");
        put.Headers.Authorization = new AuthenticationHeaderValue("Bearer", attackerAccess);
        put.Content = JsonContent.Create(body, options: JsonWeb);

        using HttpResponseMessage response = await client.SendAsync(put);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_foreign_user_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int victimId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (_, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string attackerAccess = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DefaultPassword);

        using HttpResponseMessage response = await client.SendAsync(AuthorizedDelete($"/api/v1/users/{victimId}", attackerAccess));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_foreign_user_by_email_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string victimEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (_, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string attackerAccess = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DefaultPassword);

        string path = $"/api/v1/users/by-email/{Uri.EscapeDataString(victimEmail)}";
        using HttpResponseMessage response = await client.SendAsync(AuthorizedGet(path, attackerAccess));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Put_without_bearer_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);

        object body = new
        {
            id = userId,
            name = "X",
            email,
            newPassword = (string?)null,
            currentPassword = (string?)null,
        };

        using HttpResponseMessage response = await client.PutAsJsonAsync("/api/v1/users", body, options: JsonWeb);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_unknown_email_returns_NotFound()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        string ghost = $"ghost-{Guid.NewGuid():N}@integration.hermes";
        string path = $"/api/v1/users/by-email/{Uri.EscapeDataString(ghost)}";

        using HttpResponseMessage response = await client.SendAsync(AuthorizedGet(path, access));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static HttpRequestMessage AuthorizedGet(string relativeUri, string accessToken)
    {
        HttpRequestMessage request = new(HttpMethod.Get, relativeUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static HttpRequestMessage AuthorizedDelete(string relativeUri, string accessToken)
    {
        HttpRequestMessage request = new(HttpMethod.Delete, relativeUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }
}
