using System.Net.Http.Json;
using System.Text.Json;
using Hermes.Domain.Entities;

namespace Hermes.WebFrontend.Client.Services.NewsService;

/// <summary>
/// Holds the news subscription list per user session so switching tabs does not repeat GET /list.
/// Call <see cref="Invalidate"/> on logout or when the list must be refetched from the API.
/// </summary>
public sealed class NewsSubscriptionListCache
{
    private int? _freshUserId;
    private List<News> _items = [];
    private string? _lastError;

    /// <summary>Clears cache content and error state.</summary>
    public void Invalidate()
    {
        _freshUserId = null;
        _items = [];
        _lastError = null;
    }

    /// <param name="forceReload">When true, always calls the API (after create/update/delete).</param>
    /// <summary>Returns cached items or reloads the current user's list from the API.</summary>
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
            HttpResponseMessage response = await http
                .GetAsync($"api/v1/users/news/{userId}/list", cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _lastError = await ReadErrorDetailAsync(response).ConfigureAwait(false);
                _freshUserId = null;
                _items = [];
                return (_items, _lastError);
            }

            List<News>? list = await response.Content
                .ReadFromJsonAsync<List<News>>(HermesNewsJson.Options, cancellationToken)
                .ConfigureAwait(false);
            _items = list ?? [];
            _lastError = null;
            _freshUserId = userId;
            return (_items, null);
        }
        catch (Exception ex)
        {
            _lastError = $"Laden fehlgeschlagen: {ex.Message}";
            _freshUserId = null;
            _items = [];
            return (_items, _lastError);
        }
    }

    /// <summary>Returns a detached copy of cached items.</summary>
    private List<News> Snapshot() => _items.Count == 0 ? [] : new List<News>(_items);

    /// <summary>Attempts to extract a problem-details message from a failed API response.</summary>
    private static async Task<string> ReadErrorDetailAsync(HttpResponseMessage response)
    {
        try
        {
            using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using JsonDocument doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("detail", out JsonElement d) && d.ValueKind == JsonValueKind.String)
                return d.GetString() ?? $"Fehler ({(int)response.StatusCode}).";
        }
        catch
        {
        }

        return $"Anfrage fehlgeschlagen ({(int)response.StatusCode}).";
    }
}
