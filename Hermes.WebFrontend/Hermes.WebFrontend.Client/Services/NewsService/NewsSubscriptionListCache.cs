using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using Hermes.WebFrontend.Client.ApiModels.Enums;

namespace Hermes.WebFrontend.Client.Services.NewsService;

/// <summary>
/// Loads paged news list responses from the API. <see cref="Invalidate"/> is a no-op kept for logout hooks;
/// list data is not cached between calls.
/// </summary>
public sealed class NewsSubscriptionListCache
{
    /// <summary>Clears any client-side list state (hook for logout; no in-memory list cache).</summary>
    public void Invalidate()
    {
    }

    public async Task<(NewsListPageDto? Data, string? Error)> FetchAsync(
        int userId,
        HttpClient http,
        int page,
        int pageSize,
        bool sortDescending,
        string? q,
        NewsCategory? category,
        int? afterId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            string url = BuildNewsListUrl(userId, page, pageSize, sortDescending, q, category, afterId);
            HttpResponseMessage response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (null, await ReadErrorDetailAsync(response).ConfigureAwait(false));
            }

            NewsListPageDto? dto = await response.Content
                .ReadFromJsonAsync<NewsListPageDto>(HermesNewsJsonSerializer.Options, cancellationToken)
                .ConfigureAwait(false);
            return (dto, null);
        }
        catch (Exception ex)
        {
            return (null, $"Laden fehlgeschlagen: {ex.Message}");
        }
    }

    internal static string BuildNewsListUrl(
        int userId,
        int page,
        int pageSize,
        bool sortDescending,
        string? q,
        NewsCategory? category,
        int? afterId)
    {
        StringBuilder sb = new();
        sb.Append(CultureInfo.InvariantCulture, $"api/v1/users/{userId}/newsletter-subscriptions?page={page}");
        sb.Append(CultureInfo.InvariantCulture, $"&pageSize={pageSize}");
        sb.Append(sortDescending ? "&sort=-id" : "&sort=id");
        if (!string.IsNullOrWhiteSpace(q))
            sb.Append("&q=").Append(Uri.EscapeDataString(q.Trim()));
        if (category is NewsCategory c)
            sb.Append(CultureInfo.InvariantCulture, $"&category={(int)c}");
        if (afterId is int a)
            sb.Append(CultureInfo.InvariantCulture, $"&afterId={a}");
        return sb.ToString();
    }

    private static async Task<string> ReadErrorDetailAsync(HttpResponseMessage response)
    {
        try
        {
            using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using System.Text.Json.JsonDocument doc = await System.Text.Json.JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("detail", out System.Text.Json.JsonElement d) && d.ValueKind == System.Text.Json.JsonValueKind.String)
                return d.GetString() ?? $"Fehler ({(int)response.StatusCode}).";
        }
        catch
        {
        }

        return $"Anfrage fehlgeschlagen ({(int)response.StatusCode}).";
    }
}
