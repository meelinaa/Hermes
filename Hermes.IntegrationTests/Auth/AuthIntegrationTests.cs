using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Hermes.IntegrationTests.Infrastructure;

namespace Hermes.IntegrationTests.Auth;

/// <summary>Auth flows and JWT edge cases against shared MySQL-backed API (collection serializes Serilog bootstrap).</summary>
[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class AuthIntegrationTests(MySqlApiFixture fixture)
{
    private static readonly JsonSerializerOptions _jsonWeb = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Login_with_valid_credentials_returns_OK_and_tokens()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        using HttpResponseMessage response = await AuthIntegrationFlows.LoginResponseAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(userId, json.RootElement.GetProperty("userId").GetInt32());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("refreshToken").GetString()));
    }

    [Fact]
    public async Task Login_with_invalid_password_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        using HttpResponseMessage response = await AuthIntegrationFlows.LoginResponseAsync(client, email, "WrongPassword_NoMatch!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("user@test.dev", "")]
    public async Task Login_with_missing_credentials_returns_BadRequest(string nameOrEmail, string password)
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { nameOrEmail, password },
            _jsonWeb);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("application/problem", response.Content.Headers.ContentType?.MediaType ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_with_valid_refresh_token_returns_OK_and_new_tokens()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string refresh = await AuthIntegrationFlows.LoginAndGetRefreshAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpResponseMessage response = await AuthIntegrationFlows.RefreshResponseAsync(client, refresh);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("refreshToken").GetString()));
    }

    [Fact]
    public async Task Refresh_Sequential_Rotation_Works()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string refreshFromLogin = await AuthIntegrationFlows.LoginAndGetRefreshAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        string refreshAfterRotation = await AuthIntegrationFlows.RefreshAndGetNewRefreshAsync(client, refreshFromLogin);

        string refreshSecondRotation = await AuthIntegrationFlows.RefreshAndGetNewRefreshAsync(client, refreshAfterRotation);

        Assert.NotEqual(refreshFromLogin, refreshAfterRotation);
        Assert.NotEqual(refreshAfterRotation, refreshSecondRotation);
    }

    /// <summary>Replay of a rotated refresh revokes the chain; later valid-looking rotation also fails.</summary>
    [Fact]
    public async Task Refresh_Twice_WithSameToken_RevokesFamily()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);

        string tokenA = await AuthIntegrationFlows.LoginAndGetRefreshAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        string tokenB = await AuthIntegrationFlows.RefreshAndGetNewRefreshAsync(client, tokenA);

        using HttpResponseMessage replayResponse = await AuthIntegrationFlows.RefreshResponseAsync(client, tokenA);
        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);

        using HttpResponseMessage subsequentRefreshResponse = await AuthIntegrationFlows.RefreshResponseAsync(client, tokenB);
        Assert.Equal(HttpStatusCode.Unauthorized, subsequentRefreshResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_unknown_refresh_token_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await AuthIntegrationFlows.RefreshResponseAsync(
            client,
            Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_empty_refresh_token_returns_BadRequest()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken = string.Empty },
            _jsonWeb);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Protected_route_without_bearer_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);

        using HttpResponseMessage response = await client.GetAsync(new Uri($"/api/v1/users/{userId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_route_with_malformed_bearer_token_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);

        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", JwtIntegrationTestTokens.MALFORMED_JWT_MATERIAL);

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_route_with_expired_jwt_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);

        string expired = JwtIntegrationTestTokens.CreateExpiredAccessToken(userId);
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", expired);

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Wrong symmetric key ⇒ signature invalid even when structure is plausible.</summary>
    [Fact]
    public async Task Protected_route_with_jwt_signed_using_wrong_key_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);

        string forged = JwtIntegrationTestTokens.CreateTokenWithWrongSigningKey(userId);
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_route_with_jwt_having_wrong_audience_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);

        string token = JwtIntegrationTestTokens.CreateTokenWithWrongAudience(userId);
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_route_with_jwt_having_wrong_issuer_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);

        string token = JwtIntegrationTestTokens.CreateTokenWithWrongIssuer(userId);
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Authorized_request_for_foreign_user_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int victimId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (_, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string attackerAccess = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/users/{victimId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", attackerAccess);

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Logout_with_refresh_token_body_returns_NoContent_and_refresh_rejected_after()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string refresh = await AuthIntegrationFlows.LoginAndGetRefreshAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpRequestMessage logout = new(HttpMethod.Post, "/api/v1/auth/logout");
        logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        logout.Content = JsonContent.Create(new { refreshToken = refresh }, options: _jsonWeb);

        using HttpResponseMessage logoutResp = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResp.StatusCode);

        using HttpResponseMessage replay = await AuthIntegrationFlows.RefreshResponseAsync(client, refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task Logout_without_refresh_token_revokes_all_sessions()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string refresh = await AuthIntegrationFlows.LoginAndGetRefreshAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpRequestMessage logout = new(HttpMethod.Post, "/api/v1/auth/logout");
        logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        logout.Content = JsonContent.Create(new { refreshToken = (string?)null }, options: _jsonWeb);

        using HttpResponseMessage logoutResp = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResp.StatusCode);

        using HttpResponseMessage replay = await AuthIntegrationFlows.RefreshResponseAsync(client, refresh);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    /// <summary>Mismatched logout refresh must not widen to logout-all semantics.</summary>
    [Fact]
    public async Task Logout_with_foreign_refresh_token_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        using HttpRequestMessage logout = new(HttpMethod.Post, "/api/v1/auth/logout");
        logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        logout.Content = JsonContent.Create(
            new { refreshToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)) },
            options: _jsonWeb);

        using HttpResponseMessage response = await client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
