using System.Text.Json.Serialization;

namespace Hermes.Infrastructure.NewsDataIo;

public sealed class ResultsDto
{
    [JsonPropertyName("article_id")]
    public string? ArticleId { get; set; }

    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("category")]
    public List<string>? Category { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }
}
