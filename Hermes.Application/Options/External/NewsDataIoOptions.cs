using System.ComponentModel.DataAnnotations;

namespace Hermes.Application.Options.External;

/// <summary>
/// Configuration options for external NewsData.io HTTP API integration.
/// </summary>
public sealed class NewsDataIoOptions
{
    [Required]
    public string Key { get; set; } = null!;
}
