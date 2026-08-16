using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Hermes.Domain.Constants;
using Hermes.IntegrationTests.Infrastructure;

namespace Hermes.IntegrationTests.Users;

/// <summary>
/// Contains integration tests for user management endpoints,
/// verifying user registration, profile queries, password updates, delete operations, and cross-account authorization restrictions.
/// </summary>
[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class UsersCrudIntegrationTests(MySqlApiFixture fixture)
{
    private static readonly JsonSerializerOptions _jsonWeb = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Tests that anonymous user registration succeeds and returns the created user scope.
    /// </summary>
    [Fact]
    public async Task Register_anonymous_returns_OK_and_user_scope()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"users-{Guid.NewGuid():N}@integration.hermes";
        object dto = new
        {
            id = 0,
            name = "Integration Users Test",
            email,
            password = AuthIntegrationFlows.DEFAULT_PASSWORD,
            isEmailVerified = false,
            twoFactorCode = (string?)null,
            twoFactorExpiry = (DateTime?)null,
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/users", dto, options: _jsonWeb);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("userId").GetInt32() > 0);
        Assert.Equal(email, json.RootElement.GetProperty("email").GetString());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("name").GetString()));
        Assert.False(json.RootElement.GetProperty("isEmailVerified").GetBoolean());
    }

    /// <summary>
    /// Tests that attempting to register an account with an already existing email address returns HTTP 409 Conflict.
    /// </summary>
    [Fact]
    public async Task Register_duplicate_email_returns_Conflict()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        string email = $"dup-{Guid.NewGuid():N}@integration.hermes";
        object firstDto = new
        {
            id = 0,
            name = "First",
            email,
            password = AuthIntegrationFlows.DEFAULT_PASSWORD,
            isEmailVerified = false,
            twoFactorCode = (string?)null,
            twoFactorExpiry = (DateTime?)null,
        };

        using HttpResponseMessage first = await client.PostAsJsonAsync("/api/v1/users", firstDto, options: _jsonWeb);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        object secondDto = new
        {
            id = 0,
            name = "Second",
            email,
            password = AuthIntegrationFlows.DEFAULT_PASSWORD,
            isEmailVerified = false,
            twoFactorCode = (string?)null,
            twoFactorExpiry = (DateTime?)null,
        };

        using HttpResponseMessage second = await client.PostAsJsonAsync("/api/v1/users", secondDto, options: _jsonWeb);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    /// <summary>
    /// Tests that registration attempts without a password return HTTP 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task Register_without_password_returns_BadRequest()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        object dto = new
        {
            id = 0,
            name = "No Password User",
            email = $"nopwd-{Guid.NewGuid():N}@integration.hermes",
            password = string.Empty,
            isEmailVerified = false,
            twoFactorCode = (string?)null,
            twoFactorExpiry = (DateTime?)null,
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/users", dto, options: _jsonWeb);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Tests that an authenticated user can retrieve their own profile information.
    /// </summary>
    [Fact]
    public async Task Get_own_profile_returns_OK()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpResponseMessage response = await client.SendAsync(AuthorizedGet($"/api/v1/users/{userId}", access));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(userId, json.RootElement.GetProperty("userId").GetInt32());
        Assert.Equal(email, json.RootElement.GetProperty("email").GetString());
    }

    /// <summary>
    /// Tests that an authenticated user can retrieve their own profile by email query route.
    /// </summary>
    [Fact]
    public async Task Get_own_profile_by_email_returns_OK()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        string path = $"/api/v1/users/by-email/{Uri.EscapeDataString(email)}";
        using HttpResponseMessage response = await client.SendAsync(AuthorizedGet(path, access));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(userId, json.RootElement.GetProperty("userId").GetInt32());
    }

    /// <summary>
    /// Tests that updating a password with an incorrect current password returns ProblemDetails with WRONG_CURRENT_PASSWORD type.
    /// </summary>
    [Fact]
    public async Task Update_password_with_wrong_current_password_returns_BadRequest_problem_type()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        object body = new
        {
            id = userId,
            name = "Pwd Change User",
            email,
            newPassword = "New_Valid_Pwd_9#",
            currentPassword = "totally-wrong-current-password",
        };

        using HttpRequestMessage put = new(HttpMethod.Put, "/api/v1/users");
        put.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        put.Content = JsonContent.Create(body, options: _jsonWeb);

        using HttpResponseMessage response = await client.SendAsync(put);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        Assert.Equal(HermesProblemTypeConstants.WRONG_CURRENT_PASSWORD, root.GetProperty("type").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("detail").GetString()));
    }

    /// <summary>
    /// Tests that updating a password with the correct current password succeeds and invalidates the previous password.
    /// </summary>
    [Fact]
    public async Task Update_password_with_correct_current_password_succeeds_and_login_with_new_password()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        const string NEW_PASSWORD = "Replacement_Valid_8$";
        object body = new
        {
            id = userId,
            name = "Renamed After Pwd",
            email,
            newPassword = NEW_PASSWORD,
            currentPassword = AuthIntegrationFlows.DEFAULT_PASSWORD,
        };

        using HttpRequestMessage put = new(HttpMethod.Put, "/api/v1/users");
        put.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        put.Content = JsonContent.Create(body, options: _jsonWeb);

        using HttpResponseMessage putResp = await client.SendAsync(put);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        using HttpResponseMessage oldLogin = await AuthIntegrationFlows.LoginResponseAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        using HttpResponseMessage newLogin = await AuthIntegrationFlows.LoginResponseAsync(client, email, NEW_PASSWORD);
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    /// <summary>
    /// Tests that updating a user's own profile updates properties correctly in the database.
    /// </summary>
    [Fact]
    public async Task Update_own_profile_returns_OK_and_reflected_on_get()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        string newEmail = $"renamed-{Guid.NewGuid():N}@integration.hermes";
        object body = new
        {
            id = userId,
            name = "Renamed Integration User",
            email = newEmail,
            newPassword = (string?)null,
            currentPassword = (string?)null,
        };

        using HttpRequestMessage put = new(HttpMethod.Put, "/api/v1/users");
        put.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        put.Content = JsonContent.Create(body, options: _jsonWeb);

        using HttpResponseMessage putResp = await client.SendAsync(put);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);
        using (JsonDocument putJson = JsonDocument.Parse(await putResp.Content.ReadAsStringAsync()))
        {
            Assert.Equal("Renamed Integration User", putJson.RootElement.GetProperty("name").GetString());
            Assert.Equal(newEmail, putJson.RootElement.GetProperty("email").GetString());
        }

        using HttpResponseMessage getResp = await client.SendAsync(AuthorizedGet($"/api/v1/users/{userId}", access));
        getResp.EnsureSuccessStatusCode();
        using JsonDocument got = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync());
        Assert.Equal("Renamed Integration User", got.RootElement.GetProperty("name").GetString());
        Assert.Equal(newEmail, got.RootElement.GetProperty("email").GetString());
    }

    /// <summary>
    /// Tests that deleting an account removes the user and returns HTTP 404 NotFound on subsequent lookups.
    /// </summary>
    [Fact]
    public async Task Delete_own_user_then_get_returns_NotFound()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpResponseMessage deleteResp = await client.SendAsync(AuthorizedDelete($"/api/v1/users/{userId}", access));
        Assert.Equal(HttpStatusCode.OK, deleteResp.StatusCode);

        using HttpResponseMessage getResp = await client.SendAsync(AuthorizedGet($"/api/v1/users/{userId}", access));
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    /// <summary>
    /// Tests that attempting to get another user's profile by ID returns HTTP 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task Get_foreign_user_by_id_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int victimId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (_, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string attackerAccess = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpResponseMessage response = await client.SendAsync(AuthorizedGet($"/api/v1/users/{victimId}", attackerAccess));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Tests that attempting to update another user's profile returns HTTP 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task Put_foreign_user_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int victimId, string victimEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (_, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string attackerAccess = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DEFAULT_PASSWORD);

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
        put.Content = JsonContent.Create(body, options: _jsonWeb);

        using HttpResponseMessage response = await client.SendAsync(put);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Tests that attempting to delete another user's profile returns HTTP 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task Delete_foreign_user_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int victimId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (_, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string attackerAccess = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpResponseMessage response = await client.SendAsync(AuthorizedDelete($"/api/v1/users/{victimId}", attackerAccess));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Tests that attempting to query another user's profile by email returns HTTP 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task Get_foreign_user_by_email_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string victimEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (_, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string attackerAccess = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DEFAULT_PASSWORD);

        string path = $"/api/v1/users/by-email/{Uri.EscapeDataString(victimEmail)}";
        using HttpResponseMessage response = await client.SendAsync(AuthorizedGet(path, attackerAccess));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Tests that updating user profiles without an authorization bearer token returns HTTP 401 Unauthorized.
    /// </summary>
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

        using HttpResponseMessage response = await client.PutAsJsonAsync("/api/v1/users", body, options: _jsonWeb);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Tests that querying an unassociated email address returns HTTP 404 NotFound.
    /// </summary>
    [Fact]
    public async Task Get_unknown_email_returns_NotFound()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

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
