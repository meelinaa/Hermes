namespace Hermes.WebFrontend.Client.Services.User;

/// <summary>
/// Benachrichtigt Abonnenten (z. B. Home), wenn das Profil per API geändert wurde, damit UI und HTTP-Daten neu geladen werden.
/// </summary>
public sealed class UserProfileRefreshNotifier
{
    private readonly object _gate = new();
    private readonly List<Func<Task>> _handlers = new();

    public void Subscribe(Func<Task> handler)
    {
        lock (_gate)
        {
            if (!_handlers.Contains(handler))
                _handlers.Add(handler);
        }
    }

    public void Unsubscribe(Func<Task> handler)
    {
        lock (_gate)
            _handlers.Remove(handler);
    }

    public async Task NotifyAsync()
    {
        List<Func<Task>> snapshot;
        lock (_gate)
            snapshot = _handlers.ToList();

        foreach (var h in snapshot)
        {
            try
            {
                await h.Invoke().ConfigureAwait(false);
            }
            catch
            {
                // einzelne Listener dürfen die Kette nicht abbrechen
            }
        }
    }
}
