using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Hermes.Api.Hosting;

public static class OpenTelemetryWebApplicationBuilderExtensions
{
    /// <summary>Registers ASP.NET Core, HTTP client, and runtime instrumentation with optional OTLP export.</summary>
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

    private static void ConfigureOtlp(OtlpExporterOptions exporter, HermesTelemetryOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint) && Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out Uri? endpoint))
            exporter.Endpoint = endpoint;

        if (!string.IsNullOrWhiteSpace(options.OtlpHeaders))
            exporter.Headers = options.OtlpHeaders;
    }
}
