using Blazored.LocalStorage;
using Hermes.WebFrontend.Client.Services.Auth;
using Hermes.WebFrontend.Client.Services.NewsService;
using Hermes.WebFrontend.Client.Services.User;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

WebAssemblyHostBuilder? builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
builder.Configuration.AddJsonFile($"appsettings.{builder.HostEnvironment.Environment}.json", optional: true, reloadOnChange: false);

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddScoped<AuthTokenStore>();
builder.Services.AddScoped<AuthSessionService>();
builder.Services.AddScoped<AuthLogoutService>();
builder.Services.AddSingleton<UserProfileRefreshNotifier>();
builder.Services.AddScoped<NewsSubscriptionListCache>();

// Anonymous client for auth/refresh only (no Bearer). Named client avoids handler pooling issues on the authorized pipeline.
builder.Services.AddHttpClient(AuthSessionService.ANONYMOUS_HTTP_CLIENT_NAME, (sp, client) => HermesApiHttp.ConfigureBaseAddress(client, sp));

// One HttpClient + handler chain per scope — always uses current AuthTokenStore (no IHttpClientFactory pooling of DelegatingHandler).
builder.Services.AddScoped(sp =>
{
    AuthTokenStore store = sp.GetRequiredService<AuthTokenStore>();
    AuthMessageHandler pipeline = new(store) { InnerHandler = new HttpClientHandler() };
    HttpClient client = new(pipeline);
    HermesApiHttp.ConfigureBaseAddress(client, sp);
    return client;
});

await builder.Build().RunAsync();

internal static class HermesApiHttp
{
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
