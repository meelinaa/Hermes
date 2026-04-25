using System.Net.Http.Json;
using System.Text.Json;
using Hermes.Domain.Entities;

namespace Hermes.WebFrontend.Client.Services;

/// <summary>
/// Holds the news subscription list per user session so switching tabs does not repeat GET /list.
/// Call <see cref="Invalidate"/> on logout or when the list must be refetched from the API.
/// </summary>
public sealed class NewsSubscriptionListCache
{
    private int? _freshUserId;
    private List<News> _items = new();
    private string? _lastError;

    public void Invalidate()
    {
        _freshUserId = null;
        _items = new List<News>();
        _lastError = null;
    }

    /// <param name="forceReload">When true, always calls the API (after create/update/delete).</param>
    public async Task<(List<News> Items, string? Error)> GetOrLoadAsync(
        int userId,
        HttpClient http,
        bool forceReload,
        CancellationToken cancellationToken = default)
    {
        if (!forceReload && _freshUserId == userId)
            return (Snapshot(), _lastError);

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var response = await http
                .GetAsync($"api/v1/users/news/{userId}/list", cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _lastError = await ReadErrorDetailAsync(response).ConfigureAwait(false);
                _freshUserId = null;
                _items = new List<News>();
                return (_items, _lastError);
            }

            var list = await response.Content
                .ReadFromJsonAsync<List<News>>(HermesNewsJson.Options, cancellationToken)
                .ConfigureAwait(false);
            _items = list ?? new List<News>();
            _lastError = null;
            _freshUserId = userId;
            return (_items, null);
        }
        catch (Exception ex)
        {
            _lastError = $"Laden fehlgeschlagen: {ex.Message}";
            _freshUserId = null;
            _items = new List<News>();
            return (_items, _lastError);
        }
    }

    private List<News> Snapshot() => _items.Count == 0 ? new List<News>() : new List<News>(_items);

    private static async Task<string> ReadErrorDetailAsync(HttpResponseMessage response)
    {
        try
        {
            using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String)
                return d.GetString() ?? $"Fehler ({(int)response.StatusCode}).";
        }
        catch
        {
            // ignore
        }

        return $"Anfrage fehlgeschlagen ({(int)response.StatusCode}).";
    }
}
