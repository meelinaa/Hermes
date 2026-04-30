using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Hermes.IntegrationTests;

public static class AuthIntegrationFlows
{
    public const string DefaultPassword = "Integration_Auth_Pwd_1!";
    private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

    public static async Task<(int UserId, string Email)> RegisterUserAsync(HttpClient client)
    {
        string email = $"auth-{Guid.NewGuid():N}@integration.hermes";
        var dto = new // This DTO represents the user registration payload sent to the API; it includes a unique email generated for each test run to avoid conflicts, a known password for testing login, and other required fields with default or placeholder values.
        {
            id = 0,
            name = "Integration Auth User",
            email,
            passwordHash = DefaultPassword,
            isEmailVerified = false,
            twoFactorCode = (string?)null,
            twoFactorExpiry = (DateTime?)null,
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/users", dto, JsonWeb); // Send a POST request to the user registration endpoint with the DTO as the JSON body; this should create a new user in the system and return a response containing the user's ID and email, which are extracted for use in subsequent authentication tests.
        response.EnsureSuccessStatusCode();

        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        int userId = json.RootElement.GetProperty("userId").GetInt32();
        return (userId, email);
    }

    public static Task<HttpResponseMessage> LoginResponseAsync(HttpClient client, string email, string password) => // This method sends a POST request to the login endpoint with the provided email and password; it returns the raw HttpResponseMessage, allowing the caller to inspect the status code and content for assertions in different test scenarios (e.g., successful login, invalid credentials, missing fields).
        client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { nameOrEmail = email, password },
            JsonWeb);

    public static async Task<string> LoginAndGetRefreshAsync(HttpClient client, string email, string password)
    {
        using HttpResponseMessage response = await LoginResponseAsync(client, email, password);
        response.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("refreshToken").GetString()
            ?? throw new InvalidOperationException("Login response missing refreshToken.");
    }

    public static async Task<string> LoginAndGetAccessAsync(HttpClient client, string email, string password)
    {
        using HttpResponseMessage response = await LoginResponseAsync(client, email, password);
        response.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("Login response missing accessToken.");
    }

    public static Task<HttpResponseMessage> RefreshResponseAsync(HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken }, JsonWeb);

    public static async Task<string> RefreshAndGetNewRefreshAsync(HttpClient client, string refreshToken)
    {
        using HttpResponseMessage response = await RefreshResponseAsync(client, refreshToken);
        response.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("refreshToken").GetString()
            ?? throw new InvalidOperationException("Refresh response missing refreshToken.");
    }
}
