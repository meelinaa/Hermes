using System.Collections.Concurrent;

namespace Hermes.WebFrontend.Client.Services.Notifications;

/// <summary>
/// Thread-safe in-memory notification service managing toast lifecycles and timed auto-dismissal.
/// </summary>
public sealed class ToastNotificationService : IToastNotificationService, IDisposable
{
    private readonly List<ToastMessage> _toasts = [];
    private readonly ConcurrentDictionary<Guid, Timer> _timers = new();
    private readonly object _lock = new();

    /// <summary>Gets the list of currently active toast messages.</summary>
    public IReadOnlyList<ToastMessage> Toasts
    {
        get
        {
            lock (_lock)
            {
                return _toasts.ToList();
            }
        }
    }

    /// <summary>Event raised whenever toast messages are added or removed.</summary>
    public event Action? OnChange;

    /// <summary>Dispatches a toast notification with the specified parameters.</summary>
    public void Show(string message, string? title = null, ToastNotificationLevel level = ToastNotificationLevel.Info, int durationMs = 4000)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        ToastMessage toast = new()
        {
            Message = message.Trim(),
            Title = title?.Trim(),
            Level = level,
            DurationMs = durationMs
        };

        lock (_lock)
        {
            _toasts.Add(toast);
        }

        if (durationMs > 0)
        {
            Timer timer = new(
                _ => Dismiss(toast.Id),
                null,
                TimeSpan.FromMilliseconds(durationMs),
                Timeout.InfiniteTimeSpan);

            _timers.TryAdd(toast.Id, timer);
        }

        NotifyStateChanged();
    }

    /// <summary>Dispatches a success toast notification.</summary>
    public void ShowSuccess(string message, string? title = null, int durationMs = 4000) =>
        Show(message, title, ToastNotificationLevel.Success, durationMs);

    /// <summary>Dispatches an error toast notification.</summary>
    public void ShowError(string message, string? title = null, int durationMs = 5000) =>
        Show(message, title, ToastNotificationLevel.Error, durationMs);

    /// <summary>Dispatches a warning toast notification.</summary>
    public void ShowWarning(string message, string? title = null, int durationMs = 4000) =>
        Show(message, title, ToastNotificationLevel.Warning, durationMs);

    /// <summary>Dispatches an informational toast notification.</summary>
    public void ShowInfo(string message, string? title = null, int durationMs = 4000) =>
        Show(message, title, ToastNotificationLevel.Info, durationMs);

    /// <summary>Dismisses a specific toast notification by its unique ID.</summary>
    public void Dismiss(Guid id)
    {
        bool removed = false;
        lock (_lock)
        {
            int index = _toasts.FindIndex(t => t.Id == id);
            if (index >= 0)
            {
                _toasts.RemoveAt(index);
                removed = true;
            }
        }

        if (_timers.TryRemove(id, out Timer? timer))
        {
            timer.Dispose();
        }

        if (removed)
            NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    /// <summary>Disposes all active timers and clears notifications.</summary>
    public void Dispose()
    {
        foreach (var (_, timer) in _timers)
        {
            timer.Dispose();
        }
        _timers.Clear();
        lock (_lock)
        {
            _toasts.Clear();
        }
    }
}
