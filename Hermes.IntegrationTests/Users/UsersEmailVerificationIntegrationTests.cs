using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Hermes.Domain.Entities;
using Hermes.Infrastructure.Data;
using Hermes.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.IntegrationTests.Users;

/// <summary>
/// E-mail verification routes: <c>GET …/verify/{{email}}</c> and <c>POST …/verify/code</c> against MySQL.
/// </summary>
[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class UsersEmailVerificationIntegrationTests(MySqlApiFixture fixture)
{
    private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

    private static async Task SeedVerificationChallengeAsync(
        HermesApiWebApplicationFactory factory,
        int userId,
        string code,
        DateTime expiryUtc)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HermesDbContext>();
        User user = await db.Users.FirstAsync(u => u.Id == userId);
        user.TwoFactorCode = code;
        user.TwoFactorExpiry = expiryUtc;
        await db.SaveChangesAsync();
    }

    private static HttpRequestMessage AuthorizedGet(string relativeUri, string accessToken)
    {
        HttpRequestMessage request = new(HttpMethod.Get, relativeUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    [Fact]
    public async Task Get_verify_known_email_returns_OK()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        string path = $"/api/v1/users/verify/{Uri.EscapeDataString(email)}";
        using HttpResponseMessage response = await client.SendAsync(AuthorizedGet(path, access));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(email, (await response.Content.ReadAsStringAsync()).Trim());
    }

    [Fact]
    public async Task Get_verify_unknown_email_returns_NotFound()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        string ghost = $"ghost-{Guid.NewGuid():N}@integration.hermes";
        string path = $"/api/v1/users/verify/{Uri.EscapeDataString(ghost)}";
        using HttpResponseMessage response = await client.SendAsync(AuthorizedGet(path, access));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_verify_code_with_matching_challenge_returns_OK_and_sets_verified()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        await SeedVerificationChallengeAsync(fixture.Factory, userId, "654321", DateTime.UtcNow.AddMinutes(10));

        using HttpRequestMessage post = new(HttpMethod.Post, "/api/v1/users/verify/code");
        post.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        post.Content = JsonContent.Create(new { userId, code = 654321 }, options: JsonWeb);

        using HttpResponseMessage response = await client.SendAsync(post);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using HttpResponseMessage profile = await client.SendAsync(AuthorizedGet($"/api/v1/users/{userId}", access));
        profile.EnsureSuccessStatusCode();
        using JsonDocument json = JsonDocument.Parse(await profile.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("isEmailVerified").GetBoolean());
    }

    [Fact]
    public async Task Post_verify_code_wrong_digits_returns_BadRequest()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        await SeedVerificationChallengeAsync(fixture.Factory, userId, "111111", DateTime.UtcNow.AddMinutes(10));

        using HttpRequestMessage post = new(HttpMethod.Post, "/api/v1/users/verify/code");
        post.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        post.Content = JsonContent.Create(new { userId, code = 999999 }, options: JsonWeb);

        using HttpResponseMessage response = await client.SendAsync(post);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_verify_code_after_expiry_returns_BadRequest()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DefaultPassword);

        await SeedVerificationChallengeAsync(fixture.Factory, userId, "222222", DateTime.UtcNow.AddMinutes(-2));

        using HttpRequestMessage post = new(HttpMethod.Post, "/api/v1/users/verify/code");
        post.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        post.Content = JsonContent.Create(new { userId, code = 222222 }, options: JsonWeb);

        using HttpResponseMessage response = await client.SendAsync(post);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
