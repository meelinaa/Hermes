using Serilog;

namespace Hermes.Api.Hosting;

/// <summary>Console Serilog until full host/appsettings wiring (startup errors still logged).</summary>
public static class SerilogBootstrapService
{
    public static LoggerConfiguration CreateBootstrapLoggerConfiguration() =>
        new LoggerConfiguration()
            .WriteTo.Console();

    public static void InitializeGlobalLogger()
    {
        Log.Logger = CreateBootstrapLoggerConfiguration().CreateBootstrapLogger();
    }
}
