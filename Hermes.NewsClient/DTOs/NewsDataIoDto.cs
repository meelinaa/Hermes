using System.Text.Json.Serialization;

namespace Hermes.NewsClient.DTOs;

/// <summary>
/// Top-level JSON response from the NewsData.io latest-news API.
/// </summary>
public class NewsDataIoDto
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("totalResults")]
    public int TotalResults { get; set; } = 0;

    [JsonPropertyName("results")]
    public IEnumerable<ResultsDto>? Results { get; set; }
}