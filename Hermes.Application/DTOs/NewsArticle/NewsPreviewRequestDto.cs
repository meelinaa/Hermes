using Hermes.Domain.Enums;

namespace Hermes.Application.DTOs.NewsArticle;

/// <summary>
/// Criteria for querying news articles in live preview mode.
/// </summary>
public sealed class NewsPreviewRequestDto
{
    /// <summary>Gets or sets optional search terms/keywords.</summary>
    public string? Keywords { get; set; }

    /// <summary>Gets or sets the selected news categories.</summary>
    public List<NewsCategory>? Categories { get; set; }

    /// <summary>Gets or sets the selected languages.</summary>
    public List<Language>? Languages { get; set; }

    /// <summary>Gets or sets the selected countries.</summary>
    public List<Country>? Countries { get; set; }
}
