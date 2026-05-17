using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Hermes.Worker.Hosting;

public static class OpenTelemetryWorkerHostBuilderExtensions
{
    public static HostApplicationBuilder AddHermesWorkerOpenTelemetry(this HostApplicationBuilder builder)
    {
        IConfigurationSection section = builder.Configuration.GetSection(HermesWorkerTelemetryOptions.SECTION_NAME);
        if (!section.GetValue(nameof(HermesWorkerTelemetryOptions.Enabled), false))
            return builder;

        var options = section.Get<HermesWorkerTelemetryOptions>() ?? new HermesWorkerTelemetryOptions();
        string serviceName = string.IsNullOrWhiteSpace(options.ServiceName) ? "Hermes.Worker" : options.ServiceName;

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(rb => rb
                .AddService(serviceName, serviceNamespace: "Hermes", serviceVersion: null, autoGenerateServiceInstanceId: true)
                .AddAttributes(new KeyValuePair<string, object>[]
                {
                    new("deployment.environment", builder.Environment.EnvironmentName)
                }))
            .WithTracing(t => t
                .AddEntityFrameworkCoreInstrumentation()
                .AddOtlpExporter(ote => ConfigureOtlp(ote, options)))
            .WithMetrics(m => m
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(ote => ConfigureOtlp(ote, options)));

        return builder;
    }

    private static void ConfigureOtlp(OtlpExporterOptions exporter, HermesWorkerTelemetryOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint) &&
            Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out Uri? endpoint))
            exporter.Endpoint = endpoint;

        if (!string.IsNullOrWhiteSpace(options.OtlpHeaders))
            exporter.Headers = options.OtlpHeaders;
    }
}
