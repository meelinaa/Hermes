using System.Net;
using System.Text.Json;
using Hermes.IntegrationTests.Infrastructure;

namespace Hermes.IntegrationTests.Auth;

[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class AuthDtoContractIntegrationTests(MySqlApiFixture fixture)
{
    [Fact]
    public async Task Login_JSON_includes_expected_success_and_token_shape()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        using HttpResponseMessage response = await AuthIntegrationFlows.LoginResponseAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);
        response.EnsureSuccessStatusCode();
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        Assert.True(root.TryGetProperty("success", out JsonElement _));
        Assert.True(root.TryGetProperty("userId", out JsonElement _));
        Assert.True(root.TryGetProperty("accessToken", out JsonElement _));
        Assert.True(root.TryGetProperty("tokenType", out JsonElement _));
        Assert.True(root.TryGetProperty("expiresAt", out JsonElement _));
        Assert.True(root.TryGetProperty("refreshToken", out JsonElement _));
        Assert.True(root.TryGetProperty("refreshTokenExpiresAt", out JsonElement _));
    }

    [Fact]
    public async Task Refresh_JSON_includes_expected_success_and_token_shape()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string refresh = await AuthIntegrationFlows.LoginAndGetRefreshAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);
        using HttpResponseMessage response = await AuthIntegrationFlows.RefreshResponseAsync(client, refresh);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement;
        Assert.True(root.TryGetProperty("success", out JsonElement _));
        Assert.True(root.TryGetProperty("accessToken", out JsonElement _));
        Assert.True(root.TryGetProperty("tokenType", out JsonElement _));
        Assert.True(root.TryGetProperty("expiresAt", out JsonElement _));
        Assert.True(root.TryGetProperty("refreshToken", out JsonElement _));
        Assert.True(root.TryGetProperty("refreshTokenExpiresAt", out JsonElement _));
        Assert.False(root.TryGetProperty("userId", out JsonElement _), "Refresh must not silently add userId — client uses login for identity.");
    }
}
