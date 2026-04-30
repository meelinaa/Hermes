using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Hermes.IntegrationTests.Infrastructure;

namespace Hermes.IntegrationTests.Auth;

/// <summary>
/// End-to-end authentication coverage: anonymous login/refresh flows against MySQL-backed refresh rotation,
/// plus bearer JWT rejection paths through a protected controller (<see cref="Hermes.Api.Controllers.UsersController"/>).
/// </summary>
/// <remarks>
/// Uses the shared Docker-backed fixture; tests remain sequential assembly-wide so Serilog’s bootstrap logger is not frozen twice.
/// </remarks>
[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class AuthIntegrationTests(MySqlApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Successful credential validation issues access + refresh pairs persisted as hashed refresh rows in MySQL.
    /// </summary>
    [Fact]
    public async Task Login_with_valid_credentials_returns_OK_and_tokens()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client); // Register a new user to ensure we have valid credentials; this also verifies the user registration flow and database connectivity as a prerequisite for login tests.
        using HttpResponseMessage response = await AuthIntegrationFlows.LoginResponseAsync(client, email, AuthIntegrationFlows.DefaultPassword); // Attempt to log in with the registered user's email and known password; this tests the login endpoint's ability to validate credentials and issue tokens.

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); // Parse the JSON response to verify the presence and validity of the expected properties: success flag, userId, accessToken, and refreshToken.
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(userId, json.RootElement.GetProperty("userId").GetInt32());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("refreshToken").GetString()));
    }

    /// <summary>
    /// Failed credential lookup must map to HTTP 401 without leaking whether the identifier existed (service returns <see cref="Hermes.Application.Models.Login.LoginResult"/> failure).
    /// </summary>
    [Fact]
    public async Task Login_with_invalid_password_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(client); // Register a new user to ensure we have a valid email in the system; this sets up the scenario for testing login with an incorrect password while keeping the email valid.
        using HttpResponseMessage response = await AuthIntegrationFlows.LoginResponseAsync(client, email, "WrongPassword_NoMatch!");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// FluentValidation rejects empty identifiers/passwords before touching <see cref="Hermes.Domain.Interfaces.Services.IUserService"/>—expect RFC 7807 validation problems.
    /// </summary>
    [Theory]
    [InlineData("", "password")]
    [InlineData("user@test.dev", "")]
    public async Task Login_with_missing_credentials_returns_BadRequest(string nameOrEmail, string password)
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync( 
            "/api/v1/auth/login",
            new { nameOrEmail, password }, // This anonymous object represents the JSON payload sent to the login endpoint; it includes the parameters being tested (nameOrEmail and password) which are intentionally set to invalid values to trigger validation errors.
            JsonWeb);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("application/problem", response.Content.Headers.ContentType?.MediaType ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Refresh exchanges the opaque refresh secret for a rotated pair; server-side hash lookup ties the session to MySQL.
    /// </summary>
    [Fact]
    public async Task Refresh_with_valid_refresh_token_returns_OK_and_new_tokens()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string refresh = await AuthIntegrationFlows.LoginAndGetRefreshAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        using HttpResponseMessage response = await AuthIntegrationFlows.RefreshResponseAsync(client, refresh); // Attempt to refresh the access token using the valid refresh token obtained from login.

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("refreshToken").GetString()));
    }

    /// <summary>
    /// After rotation the previous refresh plaintext must be unusable—replay detection prevents stolen tokens from working indefinitely.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="Hermes.Application.Security.AuthTokenService.RotateAsync"/> which completes rotation via <see cref="Hermes.Application.Ports.IHermesDataStore.CompleteRefreshRotationAsync"/>.
    /// </remarks>
    [Fact]
    public async Task Refresh_rejects_replay_of_previous_refresh_after_rotation()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string refreshFromLogin = await AuthIntegrationFlows.LoginAndGetRefreshAsync(client, email, AuthIntegrationFlows.DefaultPassword); // Obtain the initial refresh token from login, which will be used to test the refresh flow and subsequent replay detection after rotation.

        string refreshAfterRotation = await AuthIntegrationFlows.RefreshAndGetNewRefreshAsync(client, refreshFromLogin); // Perform a refresh using the initial refresh token, which should succeed and return a new refresh token; this simulates the normal refresh flow and sets up the scenario for testing replay detection of the old token.

        using HttpResponseMessage replayOld = await AuthIntegrationFlows.RefreshResponseAsync(client, refreshFromLogin); // Attempt to refresh again using the original refresh token after it has been rotated; this should fail with an Unauthorized status, demonstrating that the old token is no longer valid and replay attacks are mitigated.
        Assert.Equal(HttpStatusCode.Unauthorized, replayOld.StatusCode);

        string refreshSecondRotation = await AuthIntegrationFlows.RefreshAndGetNewRefreshAsync(client, refreshAfterRotation); // Perform another refresh using the new refresh token obtained from the first refresh; this should succeed and return yet another new refresh token, demonstrating that the refresh flow continues to work with valid tokens while still enforcing replay detection on old tokens.

        using HttpResponseMessage replayMiddle = await AuthIntegrationFlows.RefreshResponseAsync(client, refreshAfterRotation); // Attempt to refresh again using the second refresh token after it has been rotated; this should also fail with an Unauthorized status, confirming that each refresh token is single-use and that replay detection is consistently enforced across multiple rotations.
        Assert.Equal(HttpStatusCode.Unauthorized, replayMiddle.StatusCode);

        Assert.NotEqual(refreshFromLogin, refreshAfterRotation);
        Assert.NotEqual(refreshAfterRotation, refreshSecondRotation);
    }

    /// <summary>
    /// Random refresh material never inserted into <see cref="Hermes.Domain.Entities.RefreshToken"/> must yield <c>null</c> rotation → 401 from controller.
    /// </summary>
    [Fact]
    public async Task Refresh_with_unknown_refresh_token_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await AuthIntegrationFlows.RefreshResponseAsync( // Attempt to refresh using a random refresh token that was never issued or stored in the database; this should fail with an Unauthorized status, demonstrating that the system correctly identifies and rejects unknown tokens that do not correspond to any valid session.
            client,
            Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Refresh validator requires non-whitespace body token—mirrors <see cref="Hermes.Api.Validation.RefreshRequestValidator"/>.
    /// </summary>
    [Fact]
    public async Task Refresh_with_empty_refresh_token_returns_BadRequest()
    {
        using HttpClient client = fixture.Factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync( // Attempt to refresh using an empty string as the refresh token; this should fail with a Bad Request status due to validation rules that require a non-empty token, demonstrating that the API correctly enforces input validation for the refresh endpoint.
            "/api/v1/auth/refresh",
            new { refreshToken = "" },
            JsonWeb);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Protected routes without <c>Authorization</c> bypass JWT middleware identity—must not reach controller logic with an authenticated user.
    /// </summary>
    [Fact]
    public async Task Protected_route_without_bearer_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);

        using HttpResponseMessage response = await client.GetAsync(new Uri($"/api/v1/users/{userId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Broken JWT wiring (three segments missing / garbage payload) cannot deserialize—handler rejects before controller executes.
    /// </summary>
    [Fact]
    public async Task Protected_route_with_malformed_bearer_token_returns_Unauthorized()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);

        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/users/{userId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", JwtIntegrationTestTokens.MalformedJwtMaterial); // Attempt to access a protected route using a malformed JWT token that does not conform to the expected structure (e.g., missing segments, invalid base64 encoding); this should fail with an Unauthorized status, demonstrating that the authentication middleware correctly identifies and rejects malformed tokens before they reach the controller logic.

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Tokens outside their validity window (<c>exp</c>) must fail validation even when symmetric signatures otherwise match configuration.
    /// </summary>
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

    /// <summary>
    /// HS256 validation requires matching signing key bytes—tokens minted with attacker-controlled secrets must never authorize requests.
    /// </summary>
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

    /// <summary>
    /// Audience mismatch breaks <see cref="Microsoft.IdentityModel.Tokens.TokenValidationParameters.ValidateAudience"/>—prevents tokens minted for another relying party from working here.
    /// </summary>
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

    /// <summary>
    /// Issuer mismatch mirrors stolen tokens from another issuer pretending to be Hermes—must fail issuer validation.
    /// </summary>
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

    /// <summary>
    /// Authenticated user requesting another user id hits <see cref="Hermes.Api.Http.ControllerUserExtensions.WhenCannotAccessUser"/> → HTTP 403 (distinct from unknown JWT).
    /// </summary>
    [Fact]
    public async Task Authorized_request_for_foreign_user_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int victimId, _) = await AuthIntegrationFlows.RegisterUserAsync(client);
        (_, string attackerEmail) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string attackerAccess = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, attackerEmail, AuthIntegrationFlows.DefaultPassword);

        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/v1/users/{victimId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", attackerAccess);

        using HttpResponseMessage response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
