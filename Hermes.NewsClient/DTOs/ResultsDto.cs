using System.Text.Json.Serialization;

namespace Hermes.NewsClient.DTOs;

/// <summary>
/// A single news article in a NewsData.io response.
/// </summary>
public class ResultsDto
{
    [JsonPropertyName("article_id")]
    public string? ArticleId { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("creator")]
    public List<string>? Creator { get; set; }

    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; }

    [JsonPropertyName("category")]
    public List<string>? Category { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("pub_date")]
    public string? PubDate { get; set; }          // erstmal string — siehe unten

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("source_name")]
    public string? SourceName { get; set; }

    [JsonPropertyName("duplicate")]
    public bool Duplicate { get; set; }
}