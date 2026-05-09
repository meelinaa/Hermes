using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Enrichers.Span;

namespace Hermes.Worker.Hosting;

/// <summary>Serilog wired into <see cref="HostApplicationBuilder.Logging"/> for the generic host worker model.</summary>
public static class WorkerSerilogHostBuilderExtensions
{
    public static HostApplicationBuilder UseHermesWorkerSerilog(this HostApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .Enrich.WithProperty("Application", "Hermes.Worker")
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: true);

        return builder;
    }
}
