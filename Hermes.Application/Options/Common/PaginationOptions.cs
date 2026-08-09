using System.ComponentModel.DataAnnotations;

namespace Hermes.Application.Options.Common;

/// <summary>
/// Configuration options for default and maximum query result pagination boundaries.
/// </summary>
public sealed class PaginationOptions
{
    public const string SECTION_NAME = "Pagination";

    [Range(1, 1000)]
    public int DefaultPageSize { get; set; }

    [Range(1, 10000)]
    public int MaxPageSize { get; set; }
}
