namespace Hermes.Infrastructure.NewsDataIo;

public sealed class ApiUrlParts
{
    public string? ApiKey { get; set; }
    public IEnumerable<string>? Countries { get; set; }
    public IEnumerable<string>? Languages { get; set; }
    public IEnumerable<string>? Categories { get; set; }
    public string? Timezone { get; set; } = "europe/berlin";
    public int? Image { get; set; }
    public int? RemoveDuplicate { get; set; }
    public string? Sort { get; set; } = "pubdateasc";
    public string? ExcludeField { get; set; } = "video_url,content,keywords,source_id,sentiment,sentiment_stats";
    public string? Q { get; set; }
}
