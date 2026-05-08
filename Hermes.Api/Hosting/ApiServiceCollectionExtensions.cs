using FluentValidation;
using Hangfire;
using Hangfire.MySql;
using Hermes.Api.Hangfire;
using Hermes.Api.Validation;
using Hermes.Application.Options;
using Hermes.Application.Scheduling;
using Hermes.Application.Security;
using Hermes.Application.Services;
using Hermes.Application.Ports;
using Hermes.Domain.Interfaces.Services;
using Hermes.Infrastructure.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

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
        string? connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["CONNECTION_STRING"]
            ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection or CONNECTION_STRING.");

        services.AddDbContext<HermesDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
        services.AddScoped<IHermesDataStore>(sp => sp.GetRequiredService<HermesDbContext>());
        Log.Information("Registered HermesDbContext with MySQL connection string from configuration");

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthTokenService, AuthTokenService>();
        services.AddScoped<INewsService, NewsService>();
        services.AddScoped<INotificationLogService, NotificationLogService>();
        services.Configure<HermesSiteUrlsOptions>(configuration.GetSection(HermesSiteUrlsOptions.SECTION_NAME));
        services.AddSingleton<IVerificationMailJobTrigger, HangfireVerificationMailJobTrigger>();
        Log.Information("Registered application services: UserService, AuthTokenService, NewsService, NotificationLogService");

        services.AddSingleton(_ => CreateHangfireJobStorage(configuration));
        services.AddSingleton<INewsletterSchedulerRunTrigger, HangfireNewsletterSchedulerRunTrigger>();
        Log.Information("Registered Hangfire JobStorage (MySQL) for newsletter scheduler triggers (same DB as Hermes.Worker).");

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
        string[]? allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"];
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

        bool rateLimitingEnabled = configuration.GetValue("RateLimiting:Enabled", true);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                IProblemDetailsService problemDetailsService = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

                await problemDetailsService.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests.",
                        Detail = "Rate limit exceeded. Please retry later."
                    }
                });
            };

            if (rateLimitingEnabled)
            {
                options.AddPolicy("AuthLoginPolicy", httpContext =>
                    CreateAuthPartition(httpContext, permitLimit: 8, window: TimeSpan.FromMinutes(1)));

                options.AddPolicy("AuthRefreshPolicy", httpContext =>
                    CreateAuthPartition(httpContext, permitLimit: 30, window: TimeSpan.FromMinutes(1)));
                return;
            }

            // Keep policy names available for endpoint attributes, but effectively disable throttling.
            options.AddPolicy("AuthLoginPolicy", _ => RateLimitPartition.GetNoLimiter("testing-login"));
            options.AddPolicy("AuthRefreshPolicy", _ => RateLimitPartition.GetNoLimiter("testing-refresh"));
        });
    }

    private static RateLimitPartition<string> CreateAuthPartition(HttpContext httpContext, int permitLimit, TimeSpan window)
    {
        string? ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        string? clientKey = httpContext.Request.Headers["X-Client-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(clientKey))
            clientKey = "anonymous-client";

        // Partition by both dimensions so callers are naturally segmented by source and client identity.
        string partitionKey = $"ip:{ip}|client:{clientKey.Trim()}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: partitionKey,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    }

    private static JobStorage CreateHangfireJobStorage(IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["CONNECTION_STRING"]
            ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection or CONNECTION_STRING.");
        string? hangfireConnectionRaw = configuration.GetConnectionString("Hangfire");
        string? hangfireConnection = string.IsNullOrWhiteSpace(hangfireConnectionRaw)
            ? connectionString
            : hangfireConnectionRaw;
        return new MySqlStorage(hangfireConnection, new MySqlStorageOptions
        {
            TablesPrefix = "Hangfire"
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
