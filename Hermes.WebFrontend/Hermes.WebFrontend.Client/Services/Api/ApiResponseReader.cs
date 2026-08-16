using System.Text.Json;

namespace Hermes.WebFrontend.Client.Services.Api;

/// <summary>
/// Utility helper for parsing RFC 7807/9457 ProblemDetails and HTTP error responses.
/// </summary>
public static class ApiResponseReader
{
    /// <summary>
    /// Reads and extracts problem details, error messages, and validation errors from an HTTP response.
    /// </summary>
    /// <param name="response">The HTTP response message to parse.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A tuple containing error message, problem type, and validation errors.</returns>
    public static async Task<(string ErrorMessage, string? ProblemType, Dictionary<string, string[]>? ValidationErrors)> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            if (stream.Length == 0)
                return ($"Anfrage fehlgeschlagen ({(int)response.StatusCode}).", null, null);

            using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            JsonElement root = doc.RootElement;

            string? type = null;
            string? title = null;
            string? detail = null;
            Dictionary<string, string[]>? validationErrors = null;

            if (root.TryGetProperty("type", out JsonElement ty) && ty.ValueKind == JsonValueKind.String)
                type = ty.GetString();

            if (root.TryGetProperty("title", out JsonElement ti) && ti.ValueKind == JsonValueKind.String)
                title = ti.GetString();

            if (root.TryGetProperty("detail", out JsonElement d) && d.ValueKind == JsonValueKind.String)
                detail = d.GetString();

            if (root.TryGetProperty("errors", out JsonElement errs) && errs.ValueKind == JsonValueKind.Object)
            {
                validationErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                foreach (JsonProperty prop in errs.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<string>();
                        foreach (JsonElement item in prop.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } str)
                                list.Add(str);
                        }
                        validationErrors[prop.Name] = [.. list];
                    }
                }
            }

            string message = !string.IsNullOrWhiteSpace(detail)
                ? detail
                : !string.IsNullOrWhiteSpace(title)
                    ? title
                    : $"Anfrage fehlgeschlagen ({(int)response.StatusCode}).";

            return (message, type, validationErrors);
        }
        catch
        {
            return ($"Anfrage fehlgeschlagen ({(int)response.StatusCode}).", null, null);
        }
    }
}
