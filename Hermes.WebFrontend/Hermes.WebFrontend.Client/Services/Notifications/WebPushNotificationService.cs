using Microsoft.JSInterop;

namespace Hermes.WebFrontend.Client.Services.Notifications;

/// <summary>
/// Implementation of <see cref="IWebPushNotificationService"/> leveraging JavaScript Interop to interface with the browser's Notification API.
/// </summary>
public sealed class WebPushNotificationService(IJSRuntime jsRuntime) : IWebPushNotificationService
{
    /// <inheritdoc />
    public async ValueTask<string> GetPermissionStatusAsync()
    {
        try
        {
            return await jsRuntime.InvokeAsync<string>("hermesPush.getPermission").ConfigureAwait(false);
        }
        catch
        {
            return "unsupported";
        }
    }

    /// <inheritdoc />
    public async ValueTask<string> RequestPermissionAsync()
    {
        try
        {
            return await jsRuntime.InvokeAsync<string>("hermesPush.requestPermission").ConfigureAwait(false);
        }
        catch
        {
            return "denied";
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> SendNotificationAsync(string title, string? body = null, string? icon = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        try
        {
            return await jsRuntime.InvokeAsync<bool>("hermesPush.sendNotification", title, body, icon).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }
}
