using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Hermes.Api.Options;

namespace Hermes.Api.Hosting;

/// <summary>
/// Extension methods for configuring OpenTelemetry distributed tracing and metrics on the WebApplicationBuilder host.
/// </summary>
public static class OpenTelemetryWebApplicationBuilderExtensions
{
    /// <summary>
    /// Configures OpenTelemetry distributed tracing and runtime metrics with OTLP exporter support if enabled in configuration.
    /// </summary>
    /// <param name="builder">The WebApplicationBuilder instance.</param>
    /// <returns>The modified WebApplicationBuilder instance.</returns>
    public static WebApplicationBuilder AddHermesOpenTelemetry(this WebApplicationBuilder builder)
    {
        IConfigurationSection section = builder.Configuration.GetSection(HermesTelemetryOptions.SECTION_NAME);
        if (!section.GetValue(nameof(HermesTelemetryOptions.Enabled), false))
            return builder;

        var options = section.Get<HermesTelemetryOptions>() ?? new HermesTelemetryOptions();
        string serviceName = string.IsNullOrWhiteSpace(options.ServiceName) ? "Hermes.Api" : options.ServiceName;

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(rb => rb
                .AddService(
                    serviceName,
                    serviceNamespace: "Hermes",
                    serviceVersion: null,
                    autoGenerateServiceInstanceId: true)
                .AddAttributes(
                [
                    new("deployment.environment", builder.Environment.EnvironmentName)
                ]))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation(o =>
                {
                    o.RecordException = true;
                    o.Filter = ctx => ctx.Request.Path.StartsWithSegments("/health") is false;
                })
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(ote => ConfigureOtlp(ote, options)))
            .WithMetrics(m => m
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(ote => ConfigureOtlp(ote, options)));

        return builder;
    }

    /// <summary>
    /// Configures OTLP exporter endpoint and HTTP header settings from telemetry options.
    /// </summary>
    /// <param name="exporter">The OTLP exporter options instance.</param>
    /// <param name="options">The configured Hermes telemetry options.</param>
    private static void ConfigureOtlp(OtlpExporterOptions exporter, HermesTelemetryOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint) && Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out Uri? endpoint))
            exporter.Endpoint = endpoint;

        if (!string.IsNullOrWhiteSpace(options.OtlpHeaders))
            exporter.Headers = options.OtlpHeaders;
    }
}
