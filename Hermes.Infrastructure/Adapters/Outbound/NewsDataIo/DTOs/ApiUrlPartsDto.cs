namespace Hermes.Infrastructure.Adapters.Outbound.NewsDataIo.DTOs;

/// <summary>
/// Data transfer object holding query parts for constructing NewsData.io REST API URLs.
/// </summary>
public sealed class ApiUrlPartsDto
{
    /// <summary>
    /// Gets or sets the API key required for NewsData.io authentication.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the target country codes.
    /// </summary>
    public IEnumerable<string>? Countries { get; set; }

    /// <summary>
    /// Gets or sets the target language codes.
    /// </summary>
    public IEnumerable<string>? Languages { get; set; }

    /// <summary>
    /// Gets or sets the target news categories.
    /// </summary>
    public IEnumerable<string>? Categories { get; set; }

    /// <summary>
    /// Gets or sets the query timezone parameter (defaults to "europe/berlin").
    /// </summary>
    public string? Timezone { get; set; } = "europe/berlin";

    /// <summary>
    /// Gets or sets the image filter option.
    /// </summary>
    public int? Image { get; set; }

    /// <summary>
    /// Gets or sets the duplicate removal option.
    /// </summary>
    public int? RemoveDuplicate { get; set; }

    /// <summary>
    /// Gets or sets the sort order (defaults to "pubdateasc").
    /// </summary>
    public string? Sort { get; set; } = "pubdateasc";

    /// <summary>
    /// Gets or sets fields to exclude from response (defaults to unused large fields).
    /// </summary>
    public string? ExcludeField { get; set; } = "video_url,content,keywords,source_id,sentiment,sentiment_stats";

    /// <summary>
    /// Gets or sets the search keywords query string.
    /// </summary>
    public string? Q { get; set; }
}
