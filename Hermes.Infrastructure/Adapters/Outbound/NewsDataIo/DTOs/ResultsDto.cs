using System.Text.Json.Serialization;

namespace Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.DTOs;

/// <summary>
/// Source details for an article from NewsAPI.org.
/// </summary>
public sealed class NewsApiSourceItemDto
{
    /// <summary>
    /// Gets or sets the source identifier string.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the human-readable source publication name.
    /// </summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>
/// Data transfer object representing an individual article item from external news providers.
/// </summary>
public sealed class ResultsDto
{
    /// <summary>
    /// Gets or sets the article ID.
    /// </summary>
    [JsonPropertyName("article_id")]
    public string? ArticleId { get; set; }

    /// <summary>
    /// Gets or sets the article web URL (NewsData.io format).
    /// </summary>
    [JsonPropertyName("link")]
    public string? Link { get; set; }

    /// <summary>
    /// Gets or sets the article web URL (NewsAPI.org format).
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

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
    /// Gets or sets the publication source metadata.
    /// </summary>
    [JsonPropertyName("source")]
    public NewsApiSourceItemDto? Source { get; set; }

    /// <summary>
    /// Gets or sets the article thumbnail / hero image URL (NewsData.io format).
    /// </summary>
    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Gets or sets the article hero image URL (NewsAPI.org format).
    /// </summary>
    [JsonPropertyName("urlToImage")]
    public string? UrlToImage { get; set; }

    /// <summary>
    /// Resolves the canonical full article deep-link URL across all provider formats.
    /// </summary>
    [JsonIgnore]
    public string? ResolvedLink => !string.IsNullOrWhiteSpace(Url) ? Url : Link;

    /// <summary>
    /// Resolves the canonical image URL across all provider formats.
    /// </summary>
    [JsonIgnore]
    public string? ResolvedImageUrl => !string.IsNullOrWhiteSpace(UrlToImage) ? UrlToImage : ImageUrl;
}
