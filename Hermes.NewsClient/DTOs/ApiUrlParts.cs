namespace Hermes.NewsClient.DTOs;

/// <summary>
/// Query parameters for the NewsData.io latest-news endpoint.
/// </summary>
public class ApiUrlParts
{
    /// <summary>
    /// API key (required when building a request URL).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Country codes (comma-separated in the query string).
    /// </summary>
    public IEnumerable<string>? Countries { get; set; }

    /// <summary>
    /// Language codes (comma-separated in the query string).
    /// </summary>
    public IEnumerable<string>? Languages { get; set; }

    /// <summary>
    /// Categories (comma-separated in the query string).
    /// </summary>
    public IEnumerable<string>? Categories { get; set; }

    /// <summary>
    /// Timezone filter value.
    /// </summary>
    public string? Timezone { get; set; } = "europe/berlin";

    /// <summary>
    /// Image flag; only included in the URL when set.
    /// </summary>
    public int? Image { get; set; }

    /// <summary>
    /// Remove-duplicate flag; only included in the URL when set.
    /// </summary>
    public int? RemoveDuplicate { get; set; }

    /// <summary>
    /// Sort order.
    /// </summary>
    public string? Sort { get; set; } = "pubdateasc";

    /// <summary>
    /// Comma-separated list of fields to exclude from the response.
    /// </summary>
    public string? ExcludeField { get; set; } = "video_url,content,keywords,source_id,sentiment,sentiment_stats";
}
