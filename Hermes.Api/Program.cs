using Hermes.Api.Hosting;
using Serilog;

// Minimal bootstrap logger so failures before host build are visible on the console.
SerilogBootstrap.InitializeGlobalLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Structured logging: sinks and levels from appsettings (Serilog section).
    builder.Host.UseHermesSerilog();

    builder.Services.AddHermesApiServices(builder.Configuration);

    var app = builder.Build();
    Log.Information("Built WebApplication");

    app.UseHermesApiPipeline();

    Log.Information("Hermes.Api started");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
