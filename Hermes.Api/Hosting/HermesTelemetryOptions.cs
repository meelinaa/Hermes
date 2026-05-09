namespace Hermes.Api.Hosting;

/// <summary>OpenTelemetry (OTLP) settings for traces and runtime metrics.</summary>
public sealed class HermesTelemetryOptions
{
    public const string SECTION_NAME = "OpenTelemetry";

    public bool Enabled { get; set; }

    public string ServiceName { get; set; } = "Hermes.Api";

    /// <summary>gRPC OTLP endpoint, e.g. <c>http://localhost:4317</c>. When empty, the standard <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> env var is used if set.</summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>Optional OTLP headers (exporter format), e.g. <c>Authorization=Basic ...</c>.</summary>
    public string? OtlpHeaders { get; set; }
}
