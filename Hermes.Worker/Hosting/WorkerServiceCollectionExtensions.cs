using Hangfire;
using Hangfire.MySql;
using Hermes.Application.Jobs;
using Hermes.Application.Options;
using Hermes.Application.Ports;
using Hermes.Application.Services;
using Hermes.Infrastructure.Data;
using Hermes.Infrastructure.Email;
using Hermes.Infrastructure.NewsDataIo;
using Hermes.Notifications.Receiving.Models;
using Hermes.Worker.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace Hermes.Worker.Hosting;

public static class WorkerServiceCollectionExtensions
{
    /// <summary>
    /// Registers EF Core, e-mail, NewsData.io, Hangfire storage/server, and worker-scoped jobs.
    /// NewsData.io API key is read only from <c>.env</c> (see <see cref="WorkerServiceCollectionHelper.TryReadNewsDataIoApiKeyFromEnvFile"/>).
    /// </summary>
    public static void AddHermesWorker(this HostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? builder.Configuration["CONNECTION_STRING"]
            ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection or CONNECTION_STRING.");

        var hangfireConnectionRaw = builder.Configuration.GetConnectionString("Hangfire");
        var hangfireConnection = string.IsNullOrWhiteSpace(hangfireConnectionRaw)
            ? connectionString
            : hangfireConnectionRaw;

        builder.Services.AddDbContext<HermesDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        builder.Services.AddScoped<IHermesDataStore>(sp => sp.GetRequiredService<HermesDbContext>());
        builder.Services.AddSingleton(WorkerServiceCollectionHelper.BindEmailSettings(builder.Configuration));
        builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
        builder.Services.Configure<MailHogSettings>(builder.Configuration.GetSection("MailHog"));
        builder.Services.Configure<NewsDataIoOptions>(opts =>
        {
            opts.ApiKey = WorkerServiceCollectionHelper.TryReadNewsDataIoApiKeyFromEnvFile(builder.Environment.ContentRootPath)
                ?? string.Empty;
        });
        builder.Services.Configure<HermesSiteUrlsOptions>(builder.Configuration.GetSection(HermesSiteUrlsOptions.SectionName));
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
