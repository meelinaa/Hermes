using System.Text;

namespace Hermes.Infrastructure.NewsDataIo;

public static class NewsDataIoUrlBuilder
{
    private const string BASE_URL = "https://newsdata.io/api/1/latest?";

    public static string Build(ApiUrlParts parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (string.IsNullOrWhiteSpace(parts.ApiKey))
            throw new ArgumentException("ApiKey is required.", nameof(parts));

        StringBuilder sb = new();
        sb.Append(BASE_URL);
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
        AppendOptionalString(sb, "q", parts.Q);
        return sb.ToString();
    }

    private static void AppendCommaSeparated(StringBuilder sb, string queryName, IEnumerable<string>? values)
    {
        if (values is null)
            return;
        List<string> filteredValues = values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToList();
        if (filteredValues.Count == 0)
            return;
        sb.Append('&').Append(queryName).Append('=').Append(Uri.EscapeDataString(string.Join(",", filteredValues)));
    }

    private static void AppendOptionalString(StringBuilder sb, string queryName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        sb.Append('&').Append(queryName).Append('=').Append(Uri.EscapeDataString(value));
    }

    private static void AppendOptionalInt(StringBuilder sb, string queryName, int? value)
    {
        if (value is null)
            return;
        sb.Append('&').Append(queryName).Append('=').Append(value.Value);
    }
}
