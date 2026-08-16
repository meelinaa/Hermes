namespace Hermes.Worker.Options;

/// <summary>
/// Options for configuring OpenTelemetry within the Hermes Worker host application.
/// Provides configuration settings for OTEL tracing, metrics, and exporter endpoints.
/// </summary>
public sealed class HermesWorkerTelemetryOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SECTION_NAME = "OpenTelemetry";

    /// <summary>
    /// Gets or sets a value indicating whether OpenTelemetry export is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the OpenTelemetry service name.
    /// </summary>
    public string ServiceName { get; set; } = "Hermes.Worker";

    /// <summary>
    /// Gets or sets the OTLP exporter endpoint URI.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Gets or sets custom OTLP exporter headers.
    /// </summary>
    public string? OtlpHeaders { get; set; }

    /// <summary>
    /// Gets or sets the OTLP exporter transport protocol ('grpc' or 'http'). Defaults to 'grpc'.
    /// </summary>
    public string Protocol { get; set; } = "grpc";
}
