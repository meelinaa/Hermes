using FluentValidation;
using Hangfire;
using Hangfire.MySql;
using Hermes.Api.Hangfire;
using Hermes.Api.Validation;
using Hermes.Application.Options;
using Hermes.Application.Scheduling;
using Hermes.Application.Security;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Services;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Outbound;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.Infrastructure.Adapters.Outbound.Repositories;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Serilog.Enrichers.Span;
using Serilog;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

namespace Hermes.Api.Hosting;

public static class ApiServiceCollectionExtensions
{
    public static void AddHermesApiServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["CONNECTION_STRING"]
            ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection or CONNECTION_STRING.");

        ServerVersion serverVersion = string.Equals(environment.EnvironmentName, "Testing", StringComparison.OrdinalIgnoreCase)
            ? HermesMySqlServerVersionConstants.PinnedMysql84
            : ServerVersion.AutoDetect(connectionString);

        services.AddDbContext<HermesDbContext>(options =>
            options.UseMySql(connectionString, serverVersion));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<INewsletterSubscriptionRepository, NewsletterSubscriptionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
        Log.Information("Registered HermesDbContext with MySQL connection string from configuration");

        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthTokenService, AuthTokenService>();
        services.AddScoped<INewsletterSubscriptionService, NewsletterSubscriptionService>();
        services.AddScoped<INotificationLogService, NotificationLogService>();
        services.Configure<HermesSiteUrlsOptions>(configuration.GetSection(HermesSiteUrlsOptions.SECTION_NAME));
        services.Configure<PaginationOptions>(configuration.GetSection(PaginationOptions.SECTION_NAME));
        services.Configure<NewsletterOptions>(configuration.GetSection(NewsletterOptions.SectionName));
        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SECTION_NAME));
        services.AddHttpContextAccessor();
        services.AddSingleton<IVerificationMailJobService, VerificationMailJobService>();
        Log.Information("Registered application services: UserService, AuthTokenService, NewsletterSubscriptionService, NotificationLogService");

        services.AddHangfire((sp, config) => config
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseStorage(CreateHangfireJobStorage(configuration))
            .UseFilter(new CorrelationIdClientFilter(sp.GetRequiredService<IHttpContextAccessor>())));
        services.AddSingleton<INewsletterSchedulerJobService, NewsletterSchedulerJobService>();
        Log.Information("Registered Hangfire JobStorage (MySQL) for newsletter scheduler triggers (same DB as Hermes.Worker).");

        services.AddControllers(options =>
            {
                options.Filters.Add<AutoValidationFilter>();
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
        services.AddHermesJwtAuthentication(configuration);
        services.AddHermesOpenApiDocument(configuration);
        Log.Information("Added controllers, JWT authentication, FluentValidation, and OpenAPI services");

        services.AddProblemDetails();

        string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                                  ?? ["http://localhost:5269", "https://localhost:7016"];

        EnsureProductionAllowedOrigins(allowedOrigins, environment);

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

        services.AddHealthChecks()
            .AddDbContextCheck<HermesDbContext>("database", failureStatus: HealthStatus.Unhealthy, tags: ["ready"]);
        Log.Information("Added health checks: database with 'ready' tag");

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

                options.AddPolicy("VerifyCodePolicy", httpContext =>
                    CreateAuthPartition(httpContext, permitLimit: 10, window: TimeSpan.FromMinutes(1)));

                options.AddPolicy("VerifyMailPolicy", httpContext =>
                    CreateAuthPartition(httpContext, permitLimit: 8, window: TimeSpan.FromMinutes(1)));

                options.AddPolicy("SensitiveWritePolicy", httpContext =>
                    CreateAuthPartition(httpContext, permitLimit: 120, window: TimeSpan.FromMinutes(1)));
            }
            else
            {
                foreach (string policyName in new[]
                         {
                             "AuthLoginPolicy", "AuthRefreshPolicy", "VerifyCodePolicy", "VerifyMailPolicy", "SensitiveWritePolicy",
                         })
                {
                    options.AddPolicy(policyName, _ => RateLimitPartition.GetNoLimiter("testing-" + policyName));
                }
            }
        });
    }

    private static void EnsureProductionAllowedOrigins(string[] origins, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
            return;

        if (origins.Length == 0)
        {
            throw new InvalidOperationException(
                "Cors:AllowedOrigins must contain at least one origin in Production. Wildcards are not allowed.");
        }

        foreach (string origin in origins)
        {
            if (string.IsNullOrWhiteSpace(origin))
                throw new InvalidOperationException("Cors:AllowedOrigins cannot contain blank entries.");

            string trimmed = origin.Trim();
            if (trimmed is "*" || trimmed.Contains('*', StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Cors:AllowedOrigins must list explicit HTTPS (or localhost HTTP) origins in Production; wildcards are not permitted.");
            }

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? uri))
            {
                throw new InvalidOperationException(
                    $"Cors:AllowedOrigins value '{trimmed}' is not a valid absolute URI.");
            }

            if (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Cors:AllowedOrigins in Production expects https origins (http is only permitted for localhost). Got: '{trimmed}'.");
            }
        }
    }

    private static RateLimitPartition<string> CreateAuthPartition(HttpContext httpContext, int permitLimit, TimeSpan window)
    {
        string? ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        string? clientKey = httpContext.Request.Headers["X-Client-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(clientKey))
            clientKey = "anonymous-client";

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

    public static void UseHermesSerilog(this IHostBuilder hostBuilder)
    {
        hostBuilder.UseSerilog((context, _, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .Enrich.WithProperty("Application", "Hermes.Api"));
    }
}
