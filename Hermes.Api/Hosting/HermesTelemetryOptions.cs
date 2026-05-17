namespace Hermes.Api.Hosting;

public sealed class HermesTelemetryOptions
{
    public const string SECTION_NAME = "OpenTelemetry";

    public bool Enabled { get; set; }

    public string ServiceName { get; set; } = "Hermes.Api";

    public string? OtlpEndpoint { get; set; }

    public string? OtlpHeaders { get; set; }
}
