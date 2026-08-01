using Hangfire;
using Hangfire.MySql;
using Hermes.Application.Jobs;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Ports.Outbound;
using Hermes.Application.Ports.Inbound;
using Hermes.Application.Services;
using Hermes.Infrastructure.Adapters.Outbound.Persistence.Data;
using Hermes.Infrastructure.Adapters.Outbound.Repositories;
using Hermes.Infrastructure.Adapters.Outbound.NewsDataIo;
using Hermes.Notifications.Receiving.Models;
using Hermes.Notifications.Sending.HtmlLayout;
using Hermes.Worker.Scheduling;
using Microsoft.EntityFrameworkCore;
using Hermes.Notifications.Sending;

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
        builder.Services.AddSingleton(builder.Configuration.BindEmailOptions());
        builder.Services.AddSingleton<IEmailProvider, SmtpEmailProvider>();
        builder.Services.Configure<MailHogOptions>(builder.Configuration.GetSection("MailHog"));
        builder.Services.Configure<NewsDataIoOptions>(builder.Configuration.GetSection("NewsDataIo"));
        builder.Services.Configure<HermesSiteUrlsOptions>(builder.Configuration.GetSection(HermesSiteUrlsOptions.SECTION_NAME));
        builder.Services.Configure<NewsletterOptions>(builder.Configuration.GetSection(NewsletterOptions.SectionName));
        builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SECTION_NAME));
        builder.Services.AddHttpClient<INewsArticleProvider, NewsDataIoProvider>();
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
            })));

        builder.Services.AddHangfireServer();
    }

    internal static EmailOptions BindEmailOptions(this IConfiguration configuration)
    {
        IConfigurationSection section = configuration.GetSection("Email");
        string host = section["Host"]
            ?? throw new InvalidOperationException("Configure Email:Host (SMTP server).");
        string from = section["DefaultFromAddress"]
            ?? throw new InvalidOperationException("Configure Email:DefaultFromAddress.");
        string replyTo = section["DefaultReplyToAddress"] ?? from;
        return new EmailOptions(
            host,
            section.GetValue("Port", 25),
            section.GetValue("EnableSsl", false),
            string.IsNullOrWhiteSpace(section["Username"]) ? null : section["Username"],
            string.IsNullOrWhiteSpace(section["Password"]) ? null : section["Password"],
            from,
            section["DefaultFromName"] ?? "Hermes",
            replyTo,
            section["DefaultReplyToName"] ?? section["DefaultFromName"] ?? "Hermes",
            section["XMailer"] ?? "Hermes.Worker");
    }

    public static void LogMailHogDevHints(this IHost host)
    {
        ILogger logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Hermes.Worker");
        EmailOptions smtp = host.Services.GetRequiredService<EmailOptions>();
        logger.LogInformation(
            "SMTP: {Host}:{Port} (SSL={Ssl}), From={From} — für lokales MailHog typisch Port 1025.",
            smtp.Host,
            smtp.Port,
            smtp.EnableSsl,
            smtp.DefaultFromAddress);

        MailHogOptions? mailHog = host.Services.GetService<Microsoft.Extensions.Options.IOptions<MailHogOptions>>()?.Value;
        if (mailHog is not null && !string.IsNullOrWhiteSpace(mailHog.BaseUrl))
            logger.LogInformation("MailHog-Web-UI: {BaseUrl}", mailHog.BaseUrl.TrimEnd('/'));
    }
}
