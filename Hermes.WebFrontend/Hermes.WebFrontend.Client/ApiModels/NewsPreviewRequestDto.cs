using Hermes.WebFrontend.Client.ApiModels.Enums;

namespace Hermes.WebFrontend.Client.ApiModels;

/// <summary>
/// Request payload for live news feed preview search.
/// </summary>
public sealed class NewsPreviewRequestDto
{
    /// <summary>Gets or sets search terms/keywords.</summary>
    public string? Keywords { get; set; }

    /// <summary>Gets or sets selected category filters.</summary>
    public List<NewsCategory>? Categories { get; set; }

    /// <summary>Gets or sets selected language filters.</summary>
    public List<Language>? Languages { get; set; }

    /// <summary>Gets or sets selected country filters.</summary>
    public List<Country>? Countries { get; set; }
}
