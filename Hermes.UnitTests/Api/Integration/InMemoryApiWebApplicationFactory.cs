using System.Security.Claims;
using System.Text.Encodings.Web;
using Hermes.Api;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Hermes.UnitTests.Api.Integration;

/// <summary>
/// In-memory WebApplicationFactory for fast, isolated HTTP pipeline testing without Docker or external MySQL/Hangfire servers.
/// </summary>
public sealed class InMemoryApiWebApplicationFactory : WebApplicationFactory<ApiWebApplicationMarker>
{
    private readonly string _inMemoryDbName = Guid.NewGuid().ToString();

    public Mock<IUserService> UserServiceMock { get; } = new();
    public Mock<IUserAuthenticationService> AuthServiceMock { get; } = new();
    public Mock<INewsletterSubscriptionService> NewsletterServiceMock { get; } = new();
    public Mock<INewsletterSchedulerJobService> JobServiceMock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost;Database=dummy;Uid=root;Pwd=;");
        builder.UseSetting("ConnectionStrings:Hangfire", "Server=localhost;Database=dummy;Uid=root;Pwd=;");
        builder.UseSetting("Jwt:Issuer", "HermesTestIssuer");
        builder.UseSetting("Jwt:Audience", "HermesTestAudience");
        builder.UseSetting("Jwt:SigningKey", "super-secret-testing-signing-key-minimum-32-chars!");
        builder.UseSetting("Jwt:AccessTokenMinutes", "60");
        builder.UseSetting("Jwt:RefreshTokenDays", "14");
        builder.UseSetting("RateLimiting:Enabled", "false");
        builder.UseSetting("OpenTelemetry:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            // Replace DbContext with InMemory
            services.RemoveAll<DbContextOptions<HermesDbContext>>();
            services.RemoveAll<HermesDbContext>();
            services.AddDbContext<HermesDbContext>(options =>
                options.UseInMemoryDatabase(_inMemoryDbName));

            // Replace application services with mocks
            services.RemoveAll<IUserService>();
            services.AddScoped(_ => UserServiceMock.Object);

            services.RemoveAll<IUserAuthenticationService>();
            services.AddScoped(_ => AuthServiceMock.Object);

            services.RemoveAll<INewsletterSubscriptionService>();
            services.AddScoped(_ => NewsletterServiceMock.Object);

            services.RemoveAll<INewsletterSchedulerJobService>();
            services.AddSingleton(_ => JobServiceMock.Object);

            // Add Test Authentication Handler
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "TestScheme";
                options.DefaultChallengeScheme = "TestScheme";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", _ => { });
        });
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-UserId", out var userIdHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            string userId = userIdHeader.ToString();
            Claim[] claims =
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim("sub", userId),
                new Claim(ClaimTypes.Name, "TestUser"),
                new Claim(ClaimTypes.Email, "test@hermes.dev")
            ];

            ClaimsIdentity identity = new(claims, "TestScheme");
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, "TestScheme");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
