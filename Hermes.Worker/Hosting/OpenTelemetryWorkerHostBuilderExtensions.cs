using Hermes.Worker.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Hermes.Worker.Hosting;

/// <summary>
/// Host application builder extensions for configuring OpenTelemetry tracing and metrics in Hermes.Worker.
/// </summary>
public static class OpenTelemetryWorkerHostBuilderExtensions
{
    /// <summary>
    /// Registers OpenTelemetry tracing and metrics services if enabled in configuration.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The updated host application builder.</returns>
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

    /// <summary>
    /// Configures OTLP exporter options endpoint and headers from telemetry options.
    /// </summary>
    /// <param name="exporter">The OTLP exporter options.</param>
    /// <param name="options">The worker telemetry options.</param>
    private static void ConfigureOtlp(OtlpExporterOptions exporter, HermesWorkerTelemetryOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint) &&
            Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out Uri? endpoint))
            exporter.Endpoint = endpoint;

        if (!string.IsNullOrWhiteSpace(options.OtlpHeaders))
            exporter.Headers = options.OtlpHeaders;
    }
}
