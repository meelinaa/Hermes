using Hangfire;

using Hermes.Application.Options;

using Hermes.Application.Scheduling;

using Hermes.Worker.Hosting;

using Hermes.Worker.Scheduling;

using Microsoft.Extensions.DependencyInjection;

using Serilog;



WorkerSerilogBootstrap.InitializeBootstrapLogger();



try

{

    HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);



    builder.UseHermesWorkerSerilog();

    builder.AddHermesWorkerOpenTelemetry();



    builder.AddHermesWorker();



    var newsletterOpts = new NewsletterOptions();

    builder.Configuration.GetSection(NewsletterOptions.SectionName).Bind(newsletterOpts);

    TimeZoneInfo hangfireNewsletterTz = NewsletterSchedulingClock.ResolveTimeZone(newsletterOpts.TimeZoneId);



    IHost host = builder.Build();



    using (IServiceScope scope = host.Services.CreateScope())

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


