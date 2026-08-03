using Serilog;

namespace Hermes.Worker.Services.Serilog;

/// <summary>
/// Console Serilog bootstrap logger initialization service (same bootstrap pattern as Hermes.Api).
/// </summary>
public static class WorkerSerilogBootstrapService
{
    /// <summary>
    /// Initializes early console bootstrap logging before full host and appsettings configuration is loaded.
    /// </summary>
    public static void InitializeBootstrapLogger() => Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
}
