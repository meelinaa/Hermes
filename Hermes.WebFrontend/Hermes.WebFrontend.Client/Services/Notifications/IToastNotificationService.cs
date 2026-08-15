namespace Hermes.WebFrontend.Client.Services.Notifications;

/// <summary>
/// Service interface for dispatching and managing global toast notification messages.
/// </summary>
public interface IToastNotificationService
{
    /// <summary>Gets the list of currently active toast messages.</summary>
    IReadOnlyList<ToastMessage> Toasts { get; }

    /// <summary>Event raised whenever toast messages are added or removed.</summary>
    event Action? OnChange;

    /// <summary>Dispatches a toast notification with the specified parameters.</summary>
    void Show(string message, string? title = null, ToastNotificationLevel level = ToastNotificationLevel.Info, int durationMs = 4000);

    /// <summary>Dispatches a success toast notification.</summary>
    void ShowSuccess(string message, string? title = null, int durationMs = 4000);

    /// <summary>Dispatches an error toast notification.</summary>
    void ShowError(string message, string? title = null, int durationMs = 5000);

    /// <summary>Dispatches a warning toast notification.</summary>
    void ShowWarning(string message, string? title = null, int durationMs = 4000);

    /// <summary>Dispatches an informational toast notification.</summary>
    void ShowInfo(string message, string? title = null, int durationMs = 4000);

    /// <summary>Dismisses a specific toast notification by its unique ID.</summary>
    void Dismiss(Guid id);
}
