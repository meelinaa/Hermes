using Serilog;

namespace Hermes.Worker.Hosting;

/// <summary>Minimal Serilog sink before merged configuration loads.</summary>
public static class WorkerSerilogBootstrap
{
    public static void InitializeBootstrapLogger() => Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();
}
