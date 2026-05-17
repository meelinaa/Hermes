using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using Hermes.Api;

namespace Hermes.IntegrationTests.Infrastructure;

/// <summary>Full API in memory; <see cref="IWebHostBuilder.UseSetting"/> runs before DI so tests override connection/JWT; Testing pins Pomelo server version (no TCP probe at options build).</summary>
public sealed class HermesApiWebApplicationFactory(string connectionString) : WebApplicationFactory<ApiWebApplicationMarker>
{
    private readonly string _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("ConnectionStrings:Hangfire", _connectionString);

        builder.UseSetting("Jwt:Issuer", IntegrationTestAuthSettings.JWT_ISSUER);
        builder.UseSetting("Jwt:Audience", IntegrationTestAuthSettings.JWT_AUDIENCE);
        builder.UseSetting("Jwt:SigningKey", IntegrationTestAuthSettings.JWT_SIGNING_KEY);
        builder.UseSetting("Jwt:AccessTokenMinutes", "60");
        builder.UseSetting("Jwt:RefreshTokenDays", "14");
        builder.UseSetting("RateLimiting:Enabled", "false");
        builder.UseSetting("OpenTelemetry:Enabled", "false");
    }
}
