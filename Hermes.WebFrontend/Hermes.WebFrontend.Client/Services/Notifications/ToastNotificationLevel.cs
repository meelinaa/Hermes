namespace Hermes.WebFrontend.Client.Services.Notifications;

/// <summary>
/// Defines the severity levels for toast notification messages.
/// </summary>
public enum ToastNotificationLevel
{
    /// <summary>Informational feedback.</summary>
    Info,

    /// <summary>Success confirmation message.</summary>
    Success,

    /// <summary>Warning or caution message.</summary>
    Warning,

    /// <summary>Error or failure message.</summary>
    Error
}
