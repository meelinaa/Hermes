namespace Hermes.Api.Options;

/// <summary>
/// Options for configuring OpenTelemetry metrics and tracing exporters.
/// </summary>
public sealed class HermesTelemetryOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SECTION_NAME = "OpenTelemetry";

    /// <summary>
    /// Gets or sets a value indicating whether OpenTelemetry tracing and metrics are enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the service name reported to OpenTelemetry collectors.
    /// </summary>
    public string ServiceName { get; set; } = "Hermes.Api";

    /// <summary>
    /// Gets or sets the OTLP exporter endpoint URL.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Gets or sets optional OTLP exporter headers.
    /// </summary>
    public string? OtlpHeaders { get; set; }
}
