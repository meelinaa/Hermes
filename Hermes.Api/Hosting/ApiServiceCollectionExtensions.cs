using FluentValidation;
using Hermes.Api.Validation;
using Hermes.Application.Security;
using Hermes.Application.Services;
using Hermes.Domain.Interfaces.DBContext;
using Hermes.Domain.Interfaces.Services;
using Hermes.Infrastructure.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using System.Text.Json.Serialization;

namespace Hermes.Api.Hosting;

/// <summary>
/// Registers all API dependencies: database, application services, OpenAPI, CORS, health checks, and request timeouts.
/// </summary>
public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Adds Hermes API services to the DI container.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configuration">Application configuration (appsettings, environment variables).</param>
    public static void AddHermesApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["CONNECTION_STRING"]
            ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection or CONNECTION_STRING.");

        services.AddDbContext<HermesDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
        services.AddScoped<IHermesDbContext>(sp => sp.GetRequiredService<HermesDbContext>());
        Log.Information("Registered HermesDbContext with MySQL connection string from configuration");

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthTokenService, AuthTokenService>();
        services.AddScoped<INewsService, NewsService>();
        services.AddScoped<INotificationLogService, NotificationLogService>();
        Log.Information("Registered application services: UserService, AuthTokenService, NewsService, NotificationLogService");

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
        // JWT bearer validation + symmetric signing options; registers IJwtTokenIssuer for access tokens at login/refresh.
        services.AddHermesJwtAuthentication(configuration);
        services.AddOpenApi();
        Log.Information("Added controllers, JWT authentication, FluentValidation, and OpenAPI services");

        // RFC 7807 ProblemDetails for validation errors and exception handler integration.
        services.AddProblemDetails();

        // CORS: allowed origins from Cors:AllowedOrigins (array in appsettings); default for local SPA dev.
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"];
        services.AddCors(options =>
        {
            options.AddPolicy("FrontendPolicy", policy =>
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });
        Log.Information("Configured CORS with allowed origins: {AllowedOrigins}", string.Join(", ", allowedOrigins));

        // Kubernetes-style probes: "ready" tag limits which checks run on /health/ready.
        services.AddHealthChecks()
            .AddDbContextCheck<HermesDbContext>("database", failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);
        Log.Information("Added health checks: database with 'ready' tag");

        // Per-request timeouts: named policies for future endpoint-specific limits; default applies to all requests.
        services.AddRequestTimeouts(options =>
        {
            options.AddPolicy("Strict", TimeSpan.FromSeconds(5));
            options.AddPolicy("DataCruncher", TimeSpan.FromMinutes(1));

            options.DefaultPolicy = new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(30),
                WriteTimeoutResponse = async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "Timeout",
                        message = "The server took too long to respond."
                    });
                }
            };
        });
    }

    /// <summary>
    /// Configures Serilog as the sole logging provider, reading sinks and levels from configuration (e.g. appsettings.json).
    /// </summary>
    public static void UseHermesSerilog(this IHostBuilder hostBuilder)
    {
        hostBuilder.UseSerilog((context, _, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Hermes.Api"));
    }
}