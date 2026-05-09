namespace Hermes.Worker.Hosting;

/// <summary>OTLP / resource settings mirrored from Hermes.Api (same configuration section shape).</summary>
public sealed class HermesWorkerTelemetryOptions
{
    public const string SECTION_NAME = "OpenTelemetry";

    public bool Enabled { get; set; }

    public string ServiceName { get; set; } = "Hermes.Worker";

    public string? OtlpEndpoint { get; set; }

    public string? OtlpHeaders { get; set; }
}
