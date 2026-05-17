using Hangfire;
using Hangfire.MySql;
using Hermes.Application.Jobs;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Services;
using Hermes.Infrastructure.Data;
using Hermes.Infrastructure.Repositories;
using Hermes.Infrastructure.Email;
using Hermes.Infrastructure.NewsDataIo;
using Hermes.Notifications.Receiving.Models;
using Hermes.Worker.Scheduling;
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

        builder.Services.AddScoped<IUserStore, UserStore>();
        builder.Services.AddScoped<INewsStore, NewsStore>();
        builder.Services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        builder.Services.AddScoped<INotificationLogStore, NotificationLogStore>();
        builder.Services.AddSingleton(WorkerServiceCollectionHelper.BindEmailSettings(builder.Configuration));
        builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
        builder.Services.Configure<MailHogSettings>(builder.Configuration.GetSection("MailHog"));
        builder.Services.Configure<NewsDataIoOptions>(builder.Configuration.GetSection("NewsDataIo"));
        builder.Services.Configure<HermesSiteUrlsOptions>(builder.Configuration.GetSection(HermesSiteUrlsOptions.SECTION_NAME));
        builder.Services.Configure<NewsletterOptions>(builder.Configuration.GetSection(NewsletterOptions.SectionName));
        builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection(SecurityOptions.SECTION_NAME));
        builder.Services.AddHttpClient<INewsArticleProvider, NewsDataIoClient>();
        builder.Services.AddScoped<INewsletterDigestService, NewsletterDigestService>();
        builder.Services.AddScoped<IVerificationDigestService, VerificationDigestService>();
        builder.Services.AddScoped<INewsletterScheduleService, NewsletterScheduleService>();
        builder.Services.AddScoped<NotificationJobs>();
        builder.Services.AddScoped<NewsletterScheduler>();

        builder.Services.AddHangfire(configuration => configuration
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseStorage(new MySqlStorage(hangfireConnection, new MySqlStorageOptions
            {
                TablesPrefix = "Hangfire"
            })));

        builder.Services.AddHangfireServer();
    }
}
