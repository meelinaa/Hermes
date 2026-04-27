namespace Hermes.Application.Models.News;

public sealed class NewsArticleQuery
{
    public required string ApiKey { get; init; }
    public IEnumerable<string>? Countries { get; init; }
    public IEnumerable<string>? Languages { get; init; }
    public IEnumerable<string>? Categories { get; init; }
    public string? KeywordsQuery { get; init; }
    public string? Timezone { get; init; } = "europe/berlin";
    public int? Image { get; init; } = 1;
    public int? RemoveDuplicate { get; init; } = 1;
    public string? Sort { get; init; } = "pubdateasc";
    public string? ExcludeField { get; init; } = "video_url,content,keywords,source_id,sentiment,sentiment_stats";
}
