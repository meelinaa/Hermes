namespace Hermes.WebFrontend.Client.Services.User;

/// <summary>
/// Benachrichtigt Abonnenten (z. B. Home), wenn das Profil per API geändert wurde, damit UI und HTTP-Daten neu geladen werden.
/// </summary>
public sealed class UserProfileRefreshNotifier
{
    private readonly object _gate = new();
    private readonly List<Func<Task>> _handlers = new();

    /// <summary>Registers a refresh callback listener if it is not already registered.</summary>
    public void Subscribe(Func<Task> handler)
    {
        lock (_gate)
        {
            if (!_handlers.Contains(handler))
                _handlers.Add(handler);
        }
    }

    /// <summary>Unregisters a previously registered refresh callback listener.</summary>
    public void Unsubscribe(Func<Task> handler)
    {
        lock (_gate)
            _handlers.Remove(handler);
    }

    /// <summary>Invokes all registered listeners in sequence.</summary>
    public async Task NotifyAsync()
    {
        List<Func<Task>> snapshot;
        lock (_gate)
            snapshot = _handlers.ToList();

        foreach (Func<Task> h in snapshot)
        {
            try
            {
                await h.Invoke().ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }
}
