using System.Text.Json.Serialization;

namespace Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.DTOs;

/// <summary>
/// Data transfer object representing the top-level response payload from NewsData.io API.
/// </summary>
public sealed class NewsDataIoDto
{
    /// <summary>
    /// Gets or sets the response status string ("success" or error status).
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the total number of matching results available.
    /// </summary>
    [JsonPropertyName("totalResults")]
    public int TotalResults { get; set; }

    /// <summary>
    /// Gets or sets the collection of article result items.
    /// </summary>
    [JsonPropertyName("results")]
    public IEnumerable<ResultsDto>? Results { get; set; }
}
