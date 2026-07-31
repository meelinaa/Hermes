namespace Hermes.Application.DTOs.NewsArticle;

/// <summary>
/// Query payload containing search filters used when querying the external NewsData.io API.
/// </summary>
public sealed class NewsArticleQuery
{
    /// <summary>
    /// Gets or sets the API key for NewsData.io.
    /// </summary>
    public required string ApiKey { get; init; }

    /// <summary>
    /// Gets or sets the list of country codes (ISO 3166-1 alpha-2).
    /// </summary>
    public IEnumerable<string>? Countries { get; init; }

    /// <summary>
    /// Gets or sets the list of language codes.
    /// </summary>
    public IEnumerable<string>? Languages { get; init; }

    /// <summary>
    /// Gets or sets the list of news categories.
    /// </summary>
    public IEnumerable<string>? Categories { get; init; }

    /// <summary>
    /// Gets or sets the OR-combined search query string.
    /// </summary>
    public string? KeywordsQuery { get; init; }

    /// <summary>
    /// Gets or sets the target timezone.
    /// </summary>
    public string? Timezone { get; init; } = "europe/berlin";

    /// <summary>
    /// Gets or sets whether an image is required for the articles.
    /// </summary>
    public int? Image { get; init; } = 1;

    /// <summary>
    /// Gets or sets whether to remove duplicate articles.
    /// </summary>
    public int? RemoveDuplicate { get; init; } = 1;

    /// <summary>
    /// Gets or sets the sort order.
    /// </summary>
    public string? Sort { get; init; } = "pubdateasc";

    /// <summary>
    /// Gets or sets fields to exclude from response payloads.
    /// </summary>
    public string? ExcludeField { get; init; } = "video_url,content,keywords,source_id,sentiment,sentiment_stats";
}
