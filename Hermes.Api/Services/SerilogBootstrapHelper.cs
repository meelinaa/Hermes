using Serilog;

namespace Hermes.Api.Services;

/// <summary>
/// Console Serilog bootstrap logger service initialized prior to full host/appsettings configuration.
/// </summary>
public static class SerilogBootstrapHelper
{
    /// <summary>
    /// Creates the initial Serilog console logger configuration.
    /// </summary>
    /// <returns>A configured <see cref="LoggerConfiguration"/> instance.</returns>
    public static LoggerConfiguration CreateBootstrapLoggerConfiguration() =>
        new LoggerConfiguration()
            .WriteTo.Console();

    /// <summary>
    /// Initializes the static global Serilog logger for early application startup logging.
    /// </summary>
    public static void InitializeGlobalLogger()
    {
        Log.Logger = CreateBootstrapLoggerConfiguration().CreateBootstrapLogger();
    }
}
