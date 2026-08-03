using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Hermes.IntegrationTests;

public static class AuthIntegrationFlows
{
    public const string DEFAULT_PASSWORD = "Integration_Auth_Pwd_1!";
    private static readonly JsonSerializerOptions _jsonWeb = new(JsonSerializerDefaults.Web);

    public static async Task<(int UserId, string Email)> RegisterUserAsync(HttpClient client)
    {
        string email = $"auth-{Guid.NewGuid():N}@integration.hermes";
        var dto = new
        {
            id = 0,
            name = "Integration Auth User",
            email,
            password = DEFAULT_PASSWORD,
            isEmailVerified = false,
            twoFactorCode = (string?)null,
            twoFactorExpiry = (DateTime?)null,
        };

        using HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/users", dto, _jsonWeb);
        response.EnsureSuccessStatusCode();

        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        int userId = json.RootElement.GetProperty("userId").GetInt32();
        return (userId, email);
    }

    public static Task<HttpResponseMessage> LoginResponseAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { nameOrEmail = email, password },
            _jsonWeb);

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
        client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken }, _jsonWeb);

    public static async Task<string> RefreshAndGetNewRefreshAsync(HttpClient client, string refreshToken)
    {
        using HttpResponseMessage response = await RefreshResponseAsync(client, refreshToken);
        response.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("refreshToken").GetString()
            ?? throw new InvalidOperationException("Refresh response missing refreshToken.");
    }
}
