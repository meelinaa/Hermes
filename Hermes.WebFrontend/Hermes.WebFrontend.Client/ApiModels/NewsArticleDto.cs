namespace Hermes.WebFrontend.Client.ApiModels;

/// <summary>
/// DTO representing an article in the news feed.
/// </summary>
public sealed class NewsArticleDto
{
    /// <summary>Gets or sets the article's unique ID.</summary>
    public string? ArticleId { get; set; }

    /// <summary>Gets or sets the external URL link to the original article.</summary>
    public string? Link { get; set; }

    /// <summary>Gets or sets the article title headline.</summary>
    public string? Title { get; set; }

    /// <summary>Gets or sets the short description / snippet.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets category tags.</summary>
    public IReadOnlyList<string>? Category { get; set; }

    /// <summary>Gets or sets the thumbnail image URL.</summary>
    public string? ImageUrl { get; set; }
}
