using Hangfire;
using Serilog;

using Hermes.Application.Constants;
using Hermes.Application.Options.Newsletter;
using Hermes.Application.Services.Newsletter;
using Hermes.Worker.Hosting;
using Hermes.Worker.Services.Scheduling;
using Hermes.Worker.Services.Serilog;

WorkerSerilogBootstrapService.InitializeBootstrapLogger();

try
{
    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

    builder.UseHermesWorkerSerilog();
    builder.AddHermesWorkerOpenTelemetry();

    builder.AddHermesWorker();

    var newsletterOpts = new NewsletterOptions();
    builder.Configuration.GetSection(NewsletterOptions.SECTION_NAME).Bind(newsletterOpts);
    TimeZoneInfo hangfireNewsletterTz = NewsletterSchedulingProvider.ResolveTimeZone(newsletterOpts.TimeZoneId);

    IHost host = builder.Build();

    using (IServiceScope scope = host.Services.CreateScope())
    {
        JobStorage? storage = scope.ServiceProvider.GetService<JobStorage>();
        if (storage is not null)
            JobStorage.Current = storage;
    }

    host.LogSmtpDevHints();

    RecurringJob.AddOrUpdate<NewsletterSchedulerWorkerService>(
        RecurringJobConstants.ID,
        scheduler => scheduler.RunAsync(CancellationToken.None),
        Cron.Minutely(),
        new RecurringJobOptions { TimeZone = hangfireNewsletterTz });

    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Hermes.Worker terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
