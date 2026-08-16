namespace Hermes.WebFrontend.Client.Services.Notifications;

/// <summary>
/// Service port for interacting with the browser's Notification API and Web Push capabilities.
/// Enables user permission requests and dispatching of desktop/mobile push alerts for breaking news.
/// </summary>
public interface IWebPushNotificationService
{
    /// <summary>
    /// Checks the current browser notification permission status ('granted', 'denied', 'default', or 'unsupported').
    /// </summary>
    /// <returns>A string representation of the permission state.</returns>
    ValueTask<string> GetPermissionStatusAsync();

    /// <summary>
    /// Requests notification permission from the user via the browser prompt dialog.
    /// </summary>
    /// <returns>The resulting permission status string ('granted' or 'denied').</returns>
    ValueTask<string> RequestPermissionAsync();

    /// <summary>
    /// Dispatches a local browser notification with title, description, and optional icon.
    /// </summary>
    /// <param name="title">The notification title.</param>
    /// <param name="body">The optional notification body message.</param>
    /// <param name="icon">The optional icon URL.</param>
    /// <returns>True if notification was shown successfully; otherwise false.</returns>
    ValueTask<bool> SendNotificationAsync(string title, string? body = null, string? icon = null);
}
