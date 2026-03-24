using System.Text;
using Hermes.NewsClient.DTOs;

namespace Hermes.NewsClient;

/// <summary>
/// Builds request URLs for the NewsData.io latest-news API from <see cref="ApiUrlParts"/>.
/// </summary>
public static class NewsDataIoUrlBuilder
{
    private const string BaseUrl = "https://newsdata.io/api/1/latest?";

    /// <summary>
    /// Builds a full GET URL including query string for the NewsData.io <c>/api/v1/latest</c> endpoint.
    /// </summary>
    /// <param name="parts">Parameter values; <see cref="ApiUrlParts.ApiKey"/> must be non-empty.</param>
    /// <returns>The absolute URL with query string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="parts"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <see cref="ApiUrlParts.ApiKey"/> is null or whitespace.</exception>
    public static string Build(ApiUrlParts parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        if (string.IsNullOrWhiteSpace(parts.ApiKey))
        {
            throw new ArgumentException("ApiKey is required.", nameof(parts));
        }

        var sb = new StringBuilder();
        sb.Append(BaseUrl);
        sb.Append("apikey=");
        sb.Append(Uri.EscapeDataString(parts.ApiKey));

        AppendCommaSeparated(sb, "country", parts.Countries);
        AppendCommaSeparated(sb, "language", parts.Languages);
        AppendCommaSeparated(sb, "category", parts.Categories);
        AppendOptionalString(sb, "timezone", parts.Timezone);
        AppendOptionalInt(sb, "image", parts.Image);
        AppendOptionalInt(sb, "removeduplicate", parts.RemoveDuplicate);
        AppendOptionalString(sb, "sort", parts.Sort);
        AppendOptionalString(sb, "excludefield", parts.ExcludeField);

        return sb.ToString();
    }

    private static void AppendCommaSeparated(StringBuilder sb, string queryName, IEnumerable<string>? values)
    {
        if (values is null)
        {
            return;
        }

        var list = values.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
        if (list.Count == 0)
        {
            return;
        }

        var joined = string.Join(",", list);
        sb.Append('&').Append(queryName).Append('=').Append(Uri.EscapeDataString(joined));
    }

    private static void AppendOptionalString(StringBuilder sb, string queryName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        sb.Append('&').Append(queryName).Append('=').Append(Uri.EscapeDataString(value));
    }

    private static void AppendOptionalInt(StringBuilder sb, string queryName, int? value)
    {
        if (value is null)
        {
            return;
        }

        sb.Append('&').Append(queryName).Append('=').Append(value.Value);
    }
}
