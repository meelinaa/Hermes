using Blazored.LocalStorage;
using Hermes.WebFrontend.Client;
using Hermes.WebFrontend.Client.Services.Api;
using Hermes.WebFrontend.Client.Services.Auth;
using Hermes.WebFrontend.Client.Services.NewsService;
using Hermes.WebFrontend.Client.Services.Notifications;
using Hermes.WebFrontend.Client.Services.Theme;
using Hermes.WebFrontend.Client.Services.User;
using Hermes.WebFrontend.Client.ViewModels;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

WebAssemblyHostBuilder? builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
builder.Configuration.AddJsonFile($"appsettings.{builder.HostEnvironment.Environment}.json", optional: true, reloadOnChange: false);

builder.Services.AddHermesClientServices(builder.Configuration);

await builder.Build().RunAsync();

internal static class HermesApiHttp
{
    /// <summary>Sets the API base address from configuration or falls back to the current host base address.</summary>
    public static void ConfigureBaseAddress(HttpClient client, IServiceProvider sp)
    {
        IConfiguration config = sp.GetRequiredService<IConfiguration>();
        IWebAssemblyHostEnvironment env = sp.GetRequiredService<IWebAssemblyHostEnvironment>();
        string? baseUrl = config["ApiBaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = env.BaseAddress;
        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";

        client.BaseAddress = new Uri(baseUrl);
    }
}
