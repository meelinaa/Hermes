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
    /// Flag indicating whether OpenTelemetry distributed tracing and runtime metrics are enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Service instance name reported to OpenTelemetry collectors.
    /// </summary>
    public string ServiceName { get; set; } = "Hermes.Api";

    /// <summary>
    /// Target OTLP gRPC/HTTP collector endpoint URL.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Optional HTTP headers sent with OTLP telemetry exports.
    /// </summary>
    public string? OtlpHeaders { get; set; }
}
