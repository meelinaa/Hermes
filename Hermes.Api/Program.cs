using Hermes.Api.Hosting;
using Serilog;

SerilogBootstrap.InitializeGlobalLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddHermesOpenTelemetry();
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
