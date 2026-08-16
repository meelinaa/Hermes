using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Hermes.Domain.Entities;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.IntegrationTests.Users;

[Trait("Integration", "Docker")]
[Collection(nameof(HermesIntegrationCollection))]
public sealed class UsersEmailVerificationIntegrationTests(MySqlApiFixture fixture)
{
    private static readonly JsonSerializerOptions _jsonWeb = new(JsonSerializerDefaults.Web);

    private static async Task SeedVerificationChallengeAsync(
        HermesApiWebApplicationFactory factory,
        int userId,
        string code,
        DateTime expiryUtc)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        HermesDbContext db = scope.ServiceProvider.GetRequiredService<HermesDbContext>();
        User user = await db.Users.FirstAsync(userEntity => userEntity.Id == new Hermes.Domain.ValueObjects.UserId(userId));
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

    private static HttpRequestMessage AuthorizedPost(string relativeUri, string accessToken)
    {
        HttpRequestMessage request = new(HttpMethod.Post, relativeUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    [Fact]
    public async Task Post_verify_with_own_userid_returns_Accepted()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        string path = $"/api/v1/users/{userId}/email-verifications";
        using HttpResponseMessage response = await client.SendAsync(AuthorizedPost(path, access));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(userId, json.RootElement.GetProperty("userId").GetInt32());
        Assert.Equal(email, json.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Post_verify_with_unknown_userid_returns_Forbidden()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (_, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        int unknownUserId = int.MaxValue;
        string path = $"/api/v1/users/{unknownUserId}/email-verifications";
        using HttpResponseMessage response = await client.SendAsync(AuthorizedPost(path, access));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_verify_code_with_matching_challenge_returns_OK_and_sets_verified()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        await SeedVerificationChallengeAsync(fixture.Factory, userId, "654321", DateTime.UtcNow.AddMinutes(10));

        using HttpRequestMessage post = new(HttpMethod.Post, $"/api/v1/users/{userId}/email-verifications/confirmations");
        post.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        post.Content = JsonContent.Create(new { userId, code = 654321 }, options: _jsonWeb);

        using HttpResponseMessage response = await client.SendAsync(post);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using (JsonDocument verified = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            Assert.True(verified.RootElement.GetProperty("isEmailVerified").GetBoolean());
            Assert.Equal(userId, verified.RootElement.GetProperty("userId").GetInt32());
        }

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
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        await SeedVerificationChallengeAsync(fixture.Factory, userId, "111111", DateTime.UtcNow.AddMinutes(10));

        using HttpRequestMessage post = new(HttpMethod.Post, $"/api/v1/users/{userId}/email-verifications/confirmations");
        post.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        post.Content = JsonContent.Create(new { userId, code = 999999 }, options: _jsonWeb);

        using HttpResponseMessage response = await client.SendAsync(post);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_verify_code_after_expiry_returns_BadRequest()
    {
        using HttpClient client = fixture.Factory.CreateClient();
        (int userId, string email) = await AuthIntegrationFlows.RegisterUserAsync(client);
        string access = await AuthIntegrationFlows.LoginAndGetAccessAsync(client, email, AuthIntegrationFlows.DEFAULT_PASSWORD);

        await SeedVerificationChallengeAsync(fixture.Factory, userId, "222222", DateTime.UtcNow.AddMinutes(-2));

        using HttpRequestMessage post = new(HttpMethod.Post, $"/api/v1/users/{userId}/email-verifications/confirmations");
        post.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        post.Content = JsonContent.Create(new { userId, code = 222222 }, options: _jsonWeb);

        using HttpResponseMessage response = await client.SendAsync(post);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
