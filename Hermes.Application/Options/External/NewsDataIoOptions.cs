using System.ComponentModel.DataAnnotations;

namespace Hermes.Application.Options.External;

/// <summary>
/// Configuration options for external NewsData.io &amp; NewsAPI HTTP API integrations.
/// </summary>
public sealed class NewsDataIoOptions
{
    /// <summary>
    /// Configuration section name in appsettings.
    /// </summary>
    public const string SECTION_NAME = "NewsDataIo";

    /// <summary>
    /// The API key used to authenticate against the news provider.
    /// </summary>
    [Required]
    public string Key { get; set; } = null!;

    /// <summary>
    /// Gets or sets the maximum allowable daily requests before the client-side quota guard activates (Default: 100).
    /// </summary>
    [Range(1, 100000)]
    public int MaxDailyRequests { get; set; } = 100;
}
