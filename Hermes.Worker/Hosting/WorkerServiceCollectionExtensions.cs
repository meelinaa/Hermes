using Hangfire;
using Hangfire.MySql;
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
using Hermes.Notifications.Receiving.Options;
using Hermes.Notifications.Sending.HtmlLayout.Services;
using Hermes.Notifications.Sending.Providers;
using Hermes.Worker.Filters.Hangfire;
using Hermes.Worker.Services.Scheduling;
using Hermes.Worker.Logging;
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

        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<INewsletterSubscriptionRepository, NewsletterSubscriptionRepository>();
        builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        builder.Services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
        builder.Services.AddOptions<EmailOptions>().BindConfiguration(EmailOptions.SECTION_NAME).ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailOptions>>().Value);
        builder.Services.AddSingleton<IEmailProvider, SmtpEmailClient>();
        builder.Services.AddOptions<MailHogOptions>().BindConfiguration("MailHog").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddOptions<NewsDataIoOptions>().BindConfiguration("NewsDataIo").ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddOptions<HermesSiteUrlsOptions>().BindConfiguration(HermesSiteUrlsOptions.SECTION_NAME).ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddOptions<NewsletterOptions>().BindConfiguration(NewsletterOptions.SECTION_NAME).ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddOptions<SecurityOptions>().BindConfiguration(SecurityOptions.SECTION_NAME).ValidateDataAnnotations().ValidateOnStart();
        builder.Services.AddHttpClient<INewsArticleProvider, NewsDataIoClient>()
            .AddStandardResilienceHandler();
        builder.Services.AddSingleton<INewsletterHtmlService, NewsletterHtmlService>();
        builder.Services.AddSingleton<IVerificationHtmlService, VerificationHtmlService>();
        builder.Services.AddScoped<INewsletterDigestService, NewsletterDigestService>();
        builder.Services.AddScoped<IVerificationDigestService, VerificationDigestService>();
        builder.Services.AddScoped<INewsletterScheduleService, NewsletterScheduleService>();
        builder.Services.AddScoped<NotificationJobService>();
        builder.Services.AddScoped<NewsletterSchedulerWorkerService>();

        builder.Services.AddHangfire(configuration => configuration
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseStorage(new MySqlStorage(hangfireConnection, new MySqlStorageOptions
            {
                TablesPrefix = "Hangfire"
            }))
            .UseFilter(new CorrelationIdServerFilter()));

        builder.Services.AddHangfireServer();
    }

    public static void LogMailHogDevHints(this IHost host)
    {
        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Hermes.Worker");
        EmailOptions smtp = host.Services.GetRequiredService<EmailOptions>();
        logger.LogSmtpInfo(smtp.Host, smtp.Port, smtp.EnableSsl, smtp.DefaultFromAddress);

        MailHogOptions? mailHog = host.Services.GetService<Microsoft.Extensions.Options.IOptions<MailHogOptions>>()?.Value;
        if (mailHog is not null && !string.IsNullOrWhiteSpace(mailHog.BaseUrl))
            logger.LogMailHogWebUi(mailHog.BaseUrl.TrimEnd('/'));
    }
}
