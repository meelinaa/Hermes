namespace Hermes.Worker.Hosting;

public sealed class HermesWorkerTelemetryOptions
{
    public const string SECTION_NAME = "OpenTelemetry";

    public bool Enabled { get; set; }

    public string ServiceName { get; set; } = "Hermes.Worker";

    public string? OtlpEndpoint { get; set; }

    public string? OtlpHeaders { get; set; }
}
