using System.Text.Json.Serialization;

namespace Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.DTOs;

/// <summary>
/// Data transfer object representing an individual article item in a NewsData.io response.
/// </summary>
public sealed class ResultsDto
{
    /// <summary>
    /// Gets or sets the article ID.
    /// </summary>
    [JsonPropertyName("article_id")]
    public string? ArticleId { get; set; }

    /// <summary>
    /// Gets or sets the article web URL.
    /// </summary>
    [JsonPropertyName("link")]
    public string? Link { get; set; }

    /// <summary>
    /// Gets or sets the article title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the article description or summary.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the article category list.
    /// </summary>
    [JsonPropertyName("category")]
    public List<string>? Category { get; set; }

    /// <summary>
    /// Gets or sets the article thumbnail / hero image URL.
    /// </summary>
    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }
}
