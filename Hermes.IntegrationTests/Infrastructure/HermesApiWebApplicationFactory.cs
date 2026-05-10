using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

using Hermes.Api;

namespace Hermes.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real <strong>Hermes.Api</strong> pipeline inside an in-memory test server (<see cref="WebApplicationFactory{TEntryPoint}"/>).
/// </summary>
/// <remarks>
/// <para>
/// We inject connection strings and JWT settings via <see cref="IWebHostBuilder.UseSetting(string,string?)"/> so they participate in configuration
/// <strong>before</strong> services read values from merged configuration (same mechanism as command-line overrides). Relying only on
/// <c>appsettings.json</c> would point EF Core at a developer MySQL during host startup; in <c>Testing</c> the API pins the Pomelo MySQL capability version (<see cref="Hermes.Infrastructure.Data.HermesMySqlServerVersions.PinnedMysql84"/>) instead of probing the server at options setup time.
/// </para>
/// <para>
/// JWT settings must satisfy <see cref="Hermes.Api.Hosting.JwtAuthenticationExtensions"/> (minimum signing-key length, issuer, audience).
/// Values here are non-secret test defaults only.
/// </para>
/// </remarks>
public sealed class HermesApiWebApplicationFactory(string connectionString) : WebApplicationFactory<ApiWebApplicationMarker>
{
    private readonly string _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    protected override void ConfigureWebHost(IWebHostBuilder builder) // Runs before the real host is built, allowing us to override configuration for testing.
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
