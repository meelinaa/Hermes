using Hangfire;
using Hermes.Application.Scheduling;
using Hermes.Worker.Hosting;
using Hermes.Worker.Scheduling;

var builder = Host.CreateApplicationBuilder(args);
builder.AddHermesWorker();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    JobStorage? storage = scope.ServiceProvider.GetService<JobStorage>();
    if (storage is not null)
        JobStorage.Current = storage;
}

WorkerServiceCollectionHelper.LogMailHogDevHints(host);

RecurringJob.AddOrUpdate<NewsletterScheduler>(
    NewsletterSchedulerRecurringJob.ID,
    scheduler => scheduler.RunAsync(CancellationToken.None),
    Cron.Minutely(),
    new RecurringJobOptions { TimeZone = TimeZoneInfo.Local });

host.Run();
