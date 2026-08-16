using Hangfire;
using Hangfire.MySql;
using Polly;
using Polly.Retry;
using StackExchange.Redis;
using System.Net.Mail;
using Hermes.Application.Options.Auth;
using Hermes.Application.Options.Email;
using Hermes.Application.Options.External;
using Hermes.Application.Options.Newsletter;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Services.Newsletter;
using Hermes.Application.Services.NotificationLogs;
using Hermes.Application.Services.Users;
using Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.Providers;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.Infrastructure.Adapters.Outbound.Repositories;
using Hermes.Notifications.Sending.HtmlLayout.Services;
using Hermes.Notifications.Sending.Providers;
using Hermes.Worker.Filters.Hangfire;
using Hermes.Worker.Services.Scheduling;
using Hermes.Worker.Logging;
using Hermes.Infrastructure.Adapters.Outbound.RateLimiting;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Outbox;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Dapper;
using Hermes.Infrastructure.Adapters.Outbound.Hangfire;
using Hermes.Infrastructure.EventDispatching;
using Hermes.Application.EventHandlers;
using Hermes.Domain.Events;
using Microsoft.EntityFrameworkCore;

namespace Hermes.Worker.Hosting;

/// <summary>Worker DI: shared MySQL EF + Hangfire storage, <c>NewsDataIo</c> HttpClient from configuration.</summary>
public static class WorkerServiceCollectionExtensions
{
    public static void AddHermesWorker(this HostApplicationBuilder builder)
    {
        string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? builder.Configuration["CONNECTION_STRING"]
            ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection or CONNECTION_STRING.");

        string? hangfireConnectionRaw = builder.Configuration.GetConnectionString("Hangfire");
        string hangfireConnection = string.IsNullOrWhiteSpace(hangfireConnectionRaw)
            ? connectionString
            : hangfireConnectionRaw;

        builder.Services.AddDbContext<HermesDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        builder.Services.AddSingleton<ISqlConnectionFactory>(new MySqlConnectionFactory(connectionString));
        builder.Services.AddScoped<IUserReadQueries, UserDapperQueries>();
        builder.Services.AddScoped<INewsletterReadQueries, NewsletterDapperQueries>();
        builder.Services.AddScoped<UserRepository>();
        builder.Services.AddScoped<IUserRepository>(sp => sp.GetRequiredService<UserRepository>());
        builder.Services.AddScoped<IUserStore>(sp => sp.GetRequiredService<UserRepository>());
        builder.Services.AddScoped<IUserAuthStore>(sp => sp.GetRequiredService<UserRepository>());
        builder.Services.AddScoped<IUserVerificationStore>(sp => sp.GetRequiredService<UserRepository>());
        builder.Services.AddScoped<NewsletterSubscriptionRepository>();
        builder.Services.AddScoped<INewsletterSubscriptionRepository>(sp => sp.GetRequiredService<NewsletterSubscriptionRepository>());
        builder.Services.AddScoped<INewsletterSubscriptionStore>(sp => sp.GetRequiredService<NewsletterSubscriptionRepository>());
        builder.Services.AddScoped<INewsletterSchedulerStore>(sp => sp.GetRequiredService<NewsletterSubscriptionRepository>());
        builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        builder.Services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
        builder.Services.AddSingleton<IPasswordHasher, Hermes.Infrastructure.Adapters.Outbound.Security.BCryptPasswordHasher>();
        builder.Services.AddOptions<EmailOptions>().BindConfiguration(EmailOptions.SECTION_NAME).ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailOptions>>().Value);
        builder.Services.AddSingleton<IEmailProvider, SmtpEmailClient>();

        string? redisConnectionString = builder.Configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConnectionString))
            throw new InvalidOperationException("Configure ConnectionStrings:Redis.");

        IConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisConnectionString);
        builder.Services.AddSingleton(redis);

        builder.Services.AddResiliencePipeline("smtp-retry", pipelineBuilder =>
        {
            // Dropping requests if they exceed 5 emails per second across all workers.
            pipelineBuilder.AddRateLimiter(new RedisRateLimiter(redis, "smtp_global_ratelimit", limit: 5, window: TimeSpan.FromSeconds(1)));
            
            pipelineBuilder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<SmtpException>().Handle<IOException>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential
            });
        });
        builder.Services.AddOptions<NewsDataIoOptions>().BindConfiguration("NewsDataIo").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddOptions<HttpResilienceOptions>().BindConfiguration(HttpResilienceOptions.SECTION_NAME);
        builder.Services.AddOptions<HermesSiteUrlsOptions>().BindConfiguration(HermesSiteUrlsOptions.SECTION_NAME).ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddOptions<NewsletterOptions>().BindConfiguration(NewsletterOptions.SECTION_NAME).ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddOptions<SecurityOptions>().BindConfiguration(SecurityOptions.SECTION_NAME).ValidateDataAnnotations().ValidateOnStart();

        var resilienceOptions = builder.Configuration.GetSection(HttpResilienceOptions.SECTION_NAME).Get<HttpResilienceOptions>() ?? new HttpResilienceOptions();
        RedisRateLimiter newsApiLimiter = new(redis, "newsapi_global_ratelimit", limit: 5, window: TimeSpan.FromSeconds(1));

        builder.Services.AddHttpClient<INewsArticleProvider, NewsDataIoClient>()
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = resilienceOptions.MaxRetryAttempts;
                options.Retry.Delay = TimeSpan.FromMilliseconds(resilienceOptions.BaseDelayMilliseconds);
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(resilienceOptions.AttemptTimeoutSeconds);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(resilienceOptions.TotalRequestTimeoutSeconds);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(resilienceOptions.CircuitBreakerSamplingDurationSeconds);
                options.CircuitBreaker.FailureRatio = resilienceOptions.CircuitBreakerFailureRatio;
                options.CircuitBreaker.MinimumThroughput = resilienceOptions.CircuitBreakerMinimumThroughput;
                options.RateLimiter.RateLimiter = args => newsApiLimiter.AcquireAsync(1, args.Context.CancellationToken);
            });
        builder.Services.AddSingleton<INewsletterHtmlService, NewsletterHtmlService>();
        builder.Services.AddSingleton<IVerificationHtmlService, VerificationHtmlService>();
        builder.Services.AddScoped<IArticleFetchingService, ArticleFetchingService>();
        builder.Services.AddScoped<NewsletterDigestService>();
        builder.Services.AddScoped<INewsletterDigestService>(provider => 
            ActivatorUtilities.CreateInstance<NewsletterDigestLoggingDecorator>(provider, provider.GetRequiredService<NewsletterDigestService>()));
        builder.Services.AddScoped<VerificationDigestService>();
        builder.Services.AddScoped<IVerificationDigestService>(provider => 
            ActivatorUtilities.CreateInstance<VerificationDigestLoggingDecorator>(provider, provider.GetRequiredService<VerificationDigestService>()));
        builder.Services.AddScoped<INewsletterScheduleService, NewsletterScheduleService>();
        builder.Services.AddScoped<NotificationJobService>();
        builder.Services.AddScoped<NewsletterSchedulerWorkerService>();
        builder.Services.AddScoped<NotificationReaperWorkerService>();
        builder.Services.AddSingleton(TimeProvider.System);

        builder.Services.AddScoped<IDomainEventHandler<UserRegisteredEvent>, UserRegisteredEventHandler>();
        builder.Services.AddScoped<IDomainEventHandler<UserEmailChangedEvent>, UserEmailChangedEventHandler>();
        builder.Services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        builder.Services.AddScoped<IOutboxMessageProcessor, OutboxMessageProcessor>();
        builder.Services.AddSingleton<IVerificationMailJobService, VerificationMailJobWrapper>();

        builder.Services.AddHangfire(configuration => configuration
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseStorage(new MySqlStorage(hangfireConnection, new MySqlStorageOptions
            {
                TablesPrefix = "Hangfire"
            }))
            .UseFilter(new AutomaticRetryAttribute { Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail })
            .UseFilter(new CorrelationIdServerFilter())
            .UseFilter(new HangfireTraceContextServerFilter()));

        builder.Services.AddHangfireServer();
    }

    /// <summary>Logs SMTP connection details on startup for developer visibility.</summary>
    public static void LogSmtpDevHints(this IHost host)
    {
        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Hermes.Worker");
        EmailOptions smtp = host.Services.GetRequiredService<EmailOptions>();
        logger.LogSmtpInfo(smtp.Host, smtp.Port, smtp.EnableSsl, smtp.DefaultFromAddress);
    }
}
