using System.Net;
using System.Text.Json;
using Hermes.IntegrationTests.Infrastructure;

namespace Hermes.IntegrationTests.Auth;

[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class AuthRefreshConcurrencyIntegrationTests(MySqlApiFixture fixture)
{
    [Fact]
    public async Task Parallel_refresh_same_token_yields_exactly_one_success()
    {
        using HttpClient alpha = fixture.Factory.CreateClient();
        using HttpClient beta = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(alpha);
        string refresh = await AuthIntegrationFlows.LoginAndGetRefreshAsync(alpha, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        Task<HttpResponseMessage>[] tasks =
        [
            AuthIntegrationFlows.RefreshResponseAsync(alpha, refresh),
            AuthIntegrationFlows.RefreshResponseAsync(beta, refresh),
        ];
        HttpResponseMessage[] responses = await Task.WhenAll(tasks);
        using HttpResponseMessage r0 = responses[0];
        using HttpResponseMessage r1 = responses[1];

        int successCount = (r0.StatusCode == HttpStatusCode.OK ? 1 : 0) + (r1.StatusCode == HttpStatusCode.OK ? 1 : 0);
        int unauthorizedCount =
            (r0.StatusCode == HttpStatusCode.Unauthorized ? 1 : 0)
            + (r1.StatusCode == HttpStatusCode.Unauthorized ? 1 : 0);
        Assert.Equal(1, successCount);
        Assert.Equal(1, unauthorizedCount);

        HttpResponseMessage ok = r0.StatusCode == HttpStatusCode.OK ? r0 : r1;
        using JsonDocument doc = JsonDocument.Parse(await ok.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.False(string.IsNullOrEmpty(doc.RootElement.GetProperty("refreshToken").GetString()));
    }
}
