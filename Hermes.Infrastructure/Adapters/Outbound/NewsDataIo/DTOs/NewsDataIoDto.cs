using System.Text.Json.Serialization;

namespace Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.DTOs;

/// <summary>
/// Data transfer object representing the response payload from News API providers (NewsAPI.org and NewsData.io).
/// </summary>
public sealed class NewsDataIoDto
{
    /// <summary>
    /// Gets or sets the response status string ("ok", "success", or error status).
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the total number of matching results available.
    /// </summary>
    [JsonPropertyName("totalResults")]
    public int TotalResults { get; set; }

    /// <summary>
    /// Gets or sets the collection of article items from NewsAPI.org.
    /// </summary>
    [JsonPropertyName("articles")]
    public IEnumerable<ResultsDto>? Articles { get; set; }

    /// <summary>
    /// Gets or sets the collection of article result items from NewsData.io.
    /// </summary>
    [JsonPropertyName("results")]
    public IEnumerable<ResultsDto>? Results { get; set; }

    /// <summary>
    /// Gets all articles returned in either response format.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<ResultsDto> AllArticles => Articles ?? Results ?? [];
}
