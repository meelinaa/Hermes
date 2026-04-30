using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Hermes.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real <strong>Hermes.Api</strong> pipeline inside an in-memory test server (<see cref="WebApplicationFactory{TEntryPoint}"/>).
/// </summary>
/// <remarks>
/// <para>
/// We inject connection strings and JWT settings via <see cref="IWebHostBuilder.UseSetting(string,string?)"/> so they participate in configuration
/// <strong>before</strong> services read values from merged configuration (same mechanism as command-line overrides). Relying only on
/// <c>appsettings.json</c> would point EF Core at a developer MySQL host during host construction and cause <c>ServerVersion.AutoDetect</c>
/// to fail or hang while Docker-backed tests already expose a different server.
/// </para>
/// <para>
/// JWT settings must satisfy <see cref="Hermes.Api.Hosting.JwtAuthenticationExtensions"/> (minimum signing-key length, issuer, audience).
/// Values here are non-secret test defaults only.
/// </para>
/// </remarks>
public sealed class HermesApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public HermesApiWebApplicationFactory(string connectionString) =>
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    protected override void ConfigureWebHost(IWebHostBuilder builder) // Runs before the real host is built, allowing us to override configuration for testing.
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("ConnectionStrings:Hangfire", _connectionString);

        builder.UseSetting("Jwt:Issuer", IntegrationTestAuthSettings.JwtIssuer);
        builder.UseSetting("Jwt:Audience", IntegrationTestAuthSettings.JwtAudience);
        builder.UseSetting("Jwt:SigningKey", IntegrationTestAuthSettings.JwtSigningKey);
        builder.UseSetting("Jwt:AccessTokenMinutes", "60");
        builder.UseSetting("Jwt:RefreshTokenDays", "14");
    }
}
