using Blazored.LocalStorage;
using Hermes.WebFrontend.Client.Services.Api;
using Hermes.WebFrontend.Client.Services.Auth;
using Hermes.WebFrontend.Client.Services.NewsService;
using Hermes.WebFrontend.Client.Services.Notifications;
using Hermes.WebFrontend.Client.Services.Theme;
using Hermes.WebFrontend.Client.Services.User;
using Hermes.WebFrontend.Client.ViewModels;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hermes.WebFrontend.Client;

/// <summary>
/// Service collection extension methods for registering frontend client services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all shared Hermes client services, view models, and HTTP clients in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration for API endpoint resolution.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddHermesClientServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddBlazoredLocalStorage();
        services.AddAuthorizationCore();
        services.AddSingleton<IToastNotificationService, ToastNotificationService>();
        services.AddScoped<IThemeService, ThemeService>();
        services.AddScoped<AuthTokenStore>();
        services.AddScoped<AuthenticationStateProvider, HermesAuthenticationStateProvider>();
        services.AddScoped<AuthSessionService>();
        services.AddScoped<AuthLogoutService>();
        services.AddSingleton<UserProfileRefreshStore>();
        services.AddScoped<NewsSubscriptionApiClient>();
        services.AddScoped<INewsFeedApiClient, NewsFeedApiClient>();
        services.AddScoped<IAuthApiClient, AuthApiClient>();
        services.AddScoped<IUserApiClient, UserApiClient>();
        services.AddScoped<LoginViewModel>();
        services.AddScoped<RegisterViewModel>();
        services.AddScoped<UserSettingsViewModel>();
        services.AddScoped<NewsSettingsViewModel>();
        services.AddTransient<NewsSubscriptionCardViewModel>();
        services.AddScoped<HomeViewModel>();
        services.AddScoped<LiveFeedViewModel>();

        services.AddHttpClient(AuthSessionService.ANONYMOUS_HTTP_CLIENT_NAME, (sp, client) =>
        {
            string? baseUrl = configuration["ApiBaseUrl"]?.Trim();
            if (!string.IsNullOrWhiteSpace(baseUrl) && Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? validUri))
            {
                client.BaseAddress = validUri;
            }
            else
            {
                client.BaseAddress = new Uri("http://localhost:5165");
            }
        });

        services.AddScoped(sp =>
        {
            AuthTokenStore store = sp.GetRequiredService<AuthTokenStore>();
            AuthSessionService session = sp.GetRequiredService<AuthSessionService>();
            AuthMessageMiddleware pipeline = new(store, session) { InnerHandler = new HttpClientHandler() };
            HttpClient client = new(pipeline);
            string? baseUrl = configuration["ApiBaseUrl"]?.Trim();
            if (!string.IsNullOrWhiteSpace(baseUrl) && Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? validUri))
            {
                client.BaseAddress = validUri;
            }
            else
            {
                client.BaseAddress = new Uri("http://localhost:5165");
            }
            return client;
        });

        return services;
    }
}
