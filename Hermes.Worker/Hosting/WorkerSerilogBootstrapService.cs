using Serilog;

namespace Hermes.Worker.Hosting;

/// <summary>Console Serilog until merged host/appsettings wiring (same bootstrap idea as Hermes.Api).</summary>
public static class WorkerSerilogBootstrapService
{
    public static void InitializeBootstrapLogger() => Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
}
