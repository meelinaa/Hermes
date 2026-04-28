using System.Text.Json.Serialization;

namespace Hermes.Infrastructure.NewsDataIo;

public sealed class NewsDataIoDto
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("totalResults")]
    public int TotalResults { get; set; }

    [JsonPropertyName("results")]
    public IEnumerable<ResultsDto>? Results { get; set; }
}
